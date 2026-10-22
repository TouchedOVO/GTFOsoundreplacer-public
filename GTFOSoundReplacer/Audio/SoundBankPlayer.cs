using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GTFOSoundReplacer.Audio
{
    /// <summary>
    /// 加载自定义Wwise SoundBank音库，并用AkSoundEngine.PostEvent
    /// 播放自定义事件。bank 通过 AkSoundEngine.LoadBank(IntPtr,uint,out uint) 内存加载
    /// （GTFO-API SoundBankAPI.LoadBank 为 internal，插件无法直接调用）。
    /// </summary>
    public class SoundBankPlayer
    {
        private sealed class LoadedBank
        {
            public uint BankId;
            public IntPtr AlignedPtr; // 传给 LoadBank/UnloadBank
            public IntPtr RawPtr;     // 传给 FreeHGlobal
        }

        private readonly Dictionary<string, LoadedBank> _loadedBanks =
            new Dictionary<string, LoadedBank>(StringComparer.OrdinalIgnoreCase);

        private GameObject _emitter;
        private GameObject _listener;
        private readonly NativeRouteBackend _backend = new NativeRouteBackend();
        private readonly RouteBindingState _route;
        public SoundBankPlayer()
        {
            _route = new RouteBindingState(_backend, Log);
            lock (ListenerRouting.Gate) ListenerRouting.Add(_route);
        }

        /// <summary>用于单事件音量的 Wwise Game Parameter (RTPC) ID；0 = 未配置。</summary>
        public static uint VolumeRTPC { get; set; }

        public void SetListener(GameObject listener)
        {
            lock (ListenerRouting.Gate)
            {
                _listener = listener;
                _backend.Listener = AkSoundEngine.GetAkGameObjectID(listener);
                _route.SetFollowDefaults(listener == null);
            }
        }

        /// <summary>内存加载一个 SoundBank 文件（幂等，失败返回 false）。</summary>
        public bool LoadBank(string bankFilePath)
        {
            if (string.IsNullOrEmpty(bankFilePath) || !File.Exists(bankFilePath))
                return false;

            if (_loadedBanks.ContainsKey(bankFilePath))
                return true;

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(bankFilePath);
            }
            catch (Exception e)
            {
                Log("Failed to read SoundBank '" + bankFilePath + "': " + e.Message);
                return false;
            }

            // Wwise内存加载要求音库起始地址按16字节对齐。
            IntPtr raw = Marshal.AllocHGlobal(bytes.Length + 16);
            long aligned = (raw.ToInt64() + 15L) & ~15L;
            IntPtr alignedPtr = new IntPtr(aligned);
            Marshal.Copy(bytes, 0, alignedPtr, bytes.Length);

            AKRESULT result = AkSoundEngine.LoadBank(alignedPtr, (uint)bytes.Length, out uint bankId);
            if (result == AKRESULT.AK_Success)
            {
                _loadedBanks[bankFilePath] = new LoadedBank { BankId = bankId, AlignedPtr = alignedPtr, RawPtr = raw };
                Log("SoundBank loaded: " + bankFilePath + " (bankId=" + bankId + ")");
                return true;
            }

            Marshal.FreeHGlobal(raw);
            Log("Failed to load SoundBank '" + bankFilePath + "' (AKRESULT=" + result + ")");
            return false;
        }

        /// <summary>卸载音库并释放对应内存。</summary>
        public void UnloadBank(string bankFilePath)
        {
            if (!_loadedBanks.TryGetValue(bankFilePath, out LoadedBank bank))
                return;

            try
            {
                AkSoundEngine.UnloadBank(bank.BankId, bank.AlignedPtr);
            }
            catch
            {
                // 忽略卸载失败，仍释放内存。
            }
            Marshal.FreeHGlobal(bank.RawPtr);
            _loadedBanks.Remove(bankFilePath);
        }

        public void UnloadAll()
        {
            List<string> keys = new List<string>();
            foreach (string k in _loadedBanks.Keys) keys.Add(k);
            foreach (string k in keys) UnloadBank(k);
        }

        public bool Play(ReplacementEntry entry, Vector3 position, float volume)
        {
            if (entry == null || entry.BankEventId == 0)
                return false;

            lock (ListenerRouting.Gate)
            {
                if (ListenerRouting.IsShutdown) return false;
                GameObject go = EnsureEmitter();
                go.transform.position = position;
                if (!_route.PreparePost()) return false;
                uint playingId = AkSoundEngine.PostEvent(entry.BankEventId, go);
                if (playingId == 0) return false;
                ApplyVolume(go, playingId, volume);
                return true;
            }
        }

        /// <summary>Original preview uses the same persistent object and retains object gain.</summary>
        public uint PlayRaw(uint eventId, Vector3 position, bool voicePreview = false)
        {
            lock (ListenerRouting.Gate)
            {
                if (ListenerRouting.IsShutdown) return 0;
                GameObject go = EnsureEmitter();
                go.transform.position = position;
                if (!_route.PreparePost()) return 0;
                if (voicePreview)
                {
                    // Original voice containers have no default character. Configure only our own emitter.
                    AKRESULT character = AkSoundEngine.SetSwitch("character", "ch_01", go);
                    AKRESULT intensity = AkSoundEngine.SetSwitch("intensity_state", "intensity_1_exploration", go);
                    if (character != AKRESULT.AK_Success || intensity != AKRESULT.AK_Success)
                    {
                        Log("[PreviewOriginal] Voice switches failed: character=" + character + " intensity=" + intensity);
                        return 0;
                    }
                }
                return AkSoundEngine.PostEvent(eventId, go);
            }
        }

        private void ApplyVolume(GameObject go, uint playingId, float volume)
        {
            if (volume < 0f || Math.Abs(volume - 1f) < 0.0001f)
                return;

            try
            {
                // 没有配置RTPC时，音量作用于共享发声对象的输出连接。
                // listener=null → 作用于所有监听者（AK_INVALID_GAME_OBJECT）。
                // VolumeRTPC 为高级可选项：若配置了自定义 Game Parameter，则改用按 playingID 设置。
                if (VolumeRTPC != 0)
                    AkSoundEngine.SetRTPCValueByPlayingID(VolumeRTPC, volume, playingId);
                else
                    _route.ApplyOutputGain(volume);
            }
            catch
            {
                // Wwise 音量尽力而为：bank 未定义对应 RTPC 时静默忽略。
            }
        }

        private GameObject EnsureEmitter()
        {
            if (_emitter != null) return _emitter;
            _route.SetOwned(false);
            var go = new GameObject("GTFOSoundReplacer_SoundBankEmitter");
            UnityEngine.Object.DontDestroyOnLoad(go);
            try
            {
                AKRESULT result = AkSoundEngine.RegisterGameObj(go);
                if (result == AKRESULT.AK_Success)
                {
                    _backend.Emitter = AkSoundEngine.GetAkGameObjectID(go);
                    _route.SetOwned(true);
                }
                else Log("Emitter registration submission failed: " + result);
            }
            catch (Exception e) { Log("Emitter registration failed: " + e.Message); }

            _emitter = go;
            return go;
        }

        private static void Log(string message)
        {
            if (Plugin.Instance != null)
                Plugin.Instance.Log.LogInfo(message);
        }
    }
}

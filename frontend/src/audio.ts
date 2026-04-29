const SAMPLE_RATE = 24000;
const CHANNELS = 1;

export function createAudioHandler() {
  let audioContext: AudioContext | null = null;
  let mediaStream: MediaStream | null = null;
  let scriptProcessor: ScriptProcessorNode | null = null;
  let nextPlayTime = 0;
  let activeSources: AudioBufferSourceNode[] = [];

  return {
    async init() {
      audioContext = new AudioContext({ sampleRate: SAMPLE_RATE });
      mediaStream = await navigator.mediaDevices.getUserMedia({
        audio: {
          sampleRate: SAMPLE_RATE,
          channelCount: CHANNELS,
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true,
        },
      });
    },

    startCapture(onAudioData: (data: ArrayBuffer) => void) {
      if (!audioContext || !mediaStream) return;

      const source = audioContext.createMediaStreamSource(mediaStream);
      // 4096 frames at 24kHz ≈ 170ms chunks
      scriptProcessor = audioContext.createScriptProcessor(4096, CHANNELS, CHANNELS);

      scriptProcessor.onaudioprocess = (e) => {
        const float32 = e.inputBuffer.getChannelData(0);
        const int16 = float32ToInt16(float32);
        onAudioData(int16.buffer as ArrayBuffer);
      };

      source.connect(scriptProcessor);
      scriptProcessor.connect(audioContext.destination);
    },

    playAudio(pcm16Data: ArrayBuffer) {
      if (!audioContext) return;

      const int16 = new Int16Array(pcm16Data);
      const float32 = int16ToFloat32(int16);

      const buffer = audioContext.createBuffer(CHANNELS, float32.length, SAMPLE_RATE);
      buffer.copyToChannel(float32 as Float32Array<ArrayBuffer>, 0);

      const source = audioContext.createBufferSource();
      source.buffer = buffer;
      source.connect(audioContext.destination);

      const now = audioContext.currentTime;
      if (nextPlayTime < now) nextPlayTime = now;
      source.start(nextPlayTime);
      nextPlayTime += buffer.duration;

      activeSources.push(source);
      source.onended = () => {
        activeSources = activeSources.filter((s) => s !== source);
      };
    },

    stopPlayback() {
      for (const source of activeSources) {
        try { source.stop(); } catch { /* already stopped */ }
      }
      activeSources = [];
      nextPlayTime = 0;
    },

    dispose() {
      scriptProcessor?.disconnect();
      mediaStream?.getTracks().forEach((t) => t.stop());
      audioContext?.close();
      audioContext = null;
      mediaStream = null;
      scriptProcessor = null;
      nextPlayTime = 0;
    },
  };
}

function float32ToInt16(float32: Float32Array): Int16Array {
  const int16 = new Int16Array(float32.length);
  for (let i = 0; i < float32.length; i++) {
    const s = Math.max(-1, Math.min(1, float32[i]));
    int16[i] = s < 0 ? s * 0x8000 : s * 0x7fff;
  }
  return int16;
}

function int16ToFloat32(int16: Int16Array): Float32Array {
  const float32 = new Float32Array(int16.length);
  for (let i = 0; i < int16.length; i++) {
    float32[i] = int16[i] / 0x8000;
  }
  return float32;
}

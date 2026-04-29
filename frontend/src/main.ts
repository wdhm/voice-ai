import './style.css';
import { createAudioHandler } from './audio';
import { createWebSocketClient } from './websocket';
import { updateStatus, addBubble, updateStreamingBubble, showBaggageOptions, finalizeBubble, clearTranscript } from './ui';
import { showBooking, hideBooking } from './booking';
import { showFunctionCalling, showFunctionResult } from './system-events';

const micBtn = document.getElementById('mic-btn')!;
const muteBtn = document.getElementById('mute-btn')!;
let active = false;
let muted = false;
let audioHandler: ReturnType<typeof createAudioHandler> | null = null;
let wsClient: ReturnType<typeof createWebSocketClient> | null = null;

muteBtn.addEventListener('click', () => {
  muted = !muted;
  muteBtn.classList.toggle('active', muted);
  if (muted) {
    audioHandler?.stopPlayback();
    updateStatus('⏸ Paused — click to resume');
  } else {
    updateStatus('Listening...');
  }
});

micBtn.addEventListener('click', async () => {
  if (active) {
    stop();
    return;
  }

  try {
    audioHandler = createAudioHandler();
    await audioHandler.init();

    wsClient = createWebSocketClient({
      onAudio: (data) => {
        if (muted) return;
        setMicState('speaking');
        updateStatus('Speaking...');
        audioHandler?.playAudio(data);
      },
      onAssistantTranscript: (text) => updateStreamingBubble('bot', text),
      onUserTranscript: (text) => addBubble('user', text),
      onSessionReady: () => updateStatus('Connected — listening...'),
      onSpeechStarted: () => {
        finalizeBubble();
        setMicState('listening');
        updateStatus('Listening...');
        audioHandler?.stopPlayback();
      },
      onSpeechStopped: () => {
        setMicState('thinking');
        updateStatus('Processing...');
      },
      onFunctionCalling: (name, args) => {
        finalizeBubble();
        updateStatus(`Looking up: ${name.replace(/_/g, ' ')}...`);
        showFunctionCalling(name, args);
      },
      onFunctionResult: (name, result) => {
        showFunctionResult(name, result);
      },
      onBookingLoaded: (booking) => showBooking(booking as any),
      onBookingUpdated: (booking) => showBooking(booking as any),
      onBaggageOptions: (options) => showBaggageOptions(options as any[]),
      onError: (msg) => {
        updateStatus(`Error: ${msg}`);
        stop();
      },
    });

    await wsClient.connect();
    audioHandler.startCapture((audioData) => {
      if (!muted) wsClient?.sendAudio(audioData);
    });

    active = true;
    muteBtn.style.display = 'flex';
    setMicState('listening');
    updateStatus('Listening...');
  } catch (err) {
    updateStatus(`Failed to start: ${err}`);
    stop();
  }
});

function stop() {
  audioHandler?.dispose();
  wsClient?.disconnect();
  audioHandler = null;
  wsClient = null;
  active = false;
  muted = false;
  muteBtn.style.display = 'none';
  muteBtn.classList.remove('active');
  clearTranscript();
  hideBooking();
  setMicState('idle');
  updateStatus('Click to start');
}

function setMicState(state: 'idle' | 'listening' | 'thinking' | 'speaking') {
  micBtn.classList.remove('listening', 'thinking', 'speaking');
  if (state !== 'idle') micBtn.classList.add(state);
}

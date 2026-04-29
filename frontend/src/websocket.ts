const WS_URL = 'ws://localhost:5000/ws';

interface WebSocketCallbacks {
  onAudio: (data: ArrayBuffer) => void;
  onAssistantTranscript: (text: string) => void;
  onUserTranscript: (text: string) => void;
  onSessionReady: () => void;
  onSpeechStarted: () => void;
  onSpeechStopped: () => void;
  onFunctionCalling: (name: string, args: string) => void;
  onFunctionResult: (name: string, result: unknown) => void;
  onBookingLoaded: (booking: unknown) => void;
  onBookingUpdated: (booking: unknown) => void;
  onBaggageOptions: (options: unknown[]) => void;
  onError: (message: string) => void;
}

export function createWebSocketClient(callbacks: WebSocketCallbacks) {
  let ws: WebSocket | null = null;

  return {
    connect(): Promise<void> {
      return new Promise((resolve, reject) => {
        ws = new WebSocket(WS_URL);
        ws.binaryType = 'arraybuffer';

        ws.onopen = () => resolve();
        ws.onerror = () => reject(new Error('WebSocket connection failed'));

        ws.onclose = () => {
          ws = null;
        };

        ws.onmessage = (event) => {
          if (event.data instanceof ArrayBuffer) {
            callbacks.onAudio(event.data);
            return;
          }

          try {
            const msg = JSON.parse(event.data);
            switch (msg.type) {
              case 'session.ready':
                callbacks.onSessionReady();
                break;
              case 'speech.started':
                callbacks.onSpeechStarted();
                break;
              case 'speech.stopped':
                callbacks.onSpeechStopped();
                break;
              case 'transcript.assistant':
                callbacks.onAssistantTranscript(msg.text);
                break;
              case 'transcript.user':
                callbacks.onUserTranscript(msg.text);
                break;
              case 'function.calling':
                callbacks.onFunctionCalling(msg.name, msg.arguments ?? '');
                break;
              case 'function.result':
                callbacks.onFunctionResult(msg.name, msg.result);
                break;
              case 'booking.loaded':
                callbacks.onBookingLoaded(msg.booking);
                break;
              case 'booking.updated':
                callbacks.onBookingUpdated(msg.booking);
                break;
              case 'baggage.options':
                callbacks.onBaggageOptions(msg.options);
                break;
              case 'error':
                callbacks.onError(msg.message);
                break;
            }
          } catch {
            // Ignore non-JSON text messages
          }
        };
      });
    },

    sendAudio(data: ArrayBuffer) {
      if (ws?.readyState === WebSocket.OPEN) {
        ws.send(data);
      }
    },

    disconnect() {
      ws?.close();
      ws = null;
    },
  };
}

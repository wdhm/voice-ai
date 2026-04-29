const statusText = document.getElementById('status-text')!;
const transcript = document.getElementById('transcript')!;

let currentStreamingBubble: HTMLDivElement | null = null;
let currentStreamingRole: string | null = null;
let currentStreamingText = '';

export function updateStatus(text: string) {
  statusText.textContent = text;
}

/** Call this to close the current streaming bubble (e.g. on new turn, function call, etc.) */
export function finalizeBubble() {
  finalizeStreamingBubble();
}

export function clearTranscript() {
  finalizeStreamingBubble();
  transcript.innerHTML = '';
}

export function addBubble(role: 'user' | 'bot', text: string) {
  finalizeStreamingBubble();

  const wrapper = createMessageWrapper(role);
  const content = wrapper.querySelector('.msg-content')!;
  content.textContent = text;
  transcript.appendChild(wrapper);
  scrollToBottom();
}

export function updateStreamingBubble(role: 'user' | 'bot', textDelta: string) {
  if (currentStreamingRole && currentStreamingRole !== role) {
    finalizeStreamingBubble();
  }

  if (!currentStreamingBubble) {
    const wrapper = createMessageWrapper(role);
    currentStreamingBubble = wrapper.querySelector('.msg-content') as HTMLDivElement;
    currentStreamingRole = role;
    currentStreamingText = '';
    transcript.appendChild(wrapper);
  }

  currentStreamingText += textDelta;
  renderFormattedText(currentStreamingBubble, currentStreamingText);
  scrollToBottom();
}

function finalizeStreamingBubble() {
  currentStreamingBubble = null;
  currentStreamingRole = null;
  currentStreamingText = '';
}

function createMessageWrapper(role: 'user' | 'bot'): HTMLDivElement {
  const wrapper = document.createElement('div');
  wrapper.className = `msg ${role}`;

  const label = document.createElement('div');
  label.className = 'msg-label';
  label.textContent = role === 'user' ? 'You' : 'Assistant';

  const content = document.createElement('div');
  content.className = 'msg-content';

  wrapper.appendChild(label);
  wrapper.appendChild(content);
  return wrapper;
}

function renderFormattedText(el: HTMLDivElement, text: string) {
  // Split into paragraphs on double newlines, then handle single newlines as <br>
  const paragraphs = text.split(/\n{2,}/);
  el.innerHTML = paragraphs
    .map(p => {
      const escaped = p
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/\n/g, '<br>');
      return `<p>${escaped}</p>`;
    })
    .join('');
}

export function showBaggageOptions(options: any[]) {
  finalizeStreamingBubble();

  const card = document.createElement('div');
  card.className = 'options-card';
  card.innerHTML = `
    <div class="options-title">🧳 Available Baggage Options</div>
    <div class="options-list">
      ${options.map(o => `
        <div class="option-row">
          <span class="option-desc">${o.description}</span>
          <span class="option-price">${o.price} ${o.currency}</span>
        </div>
      `).join('')}
    </div>
  `;
  transcript.appendChild(card);
  scrollToBottom();
}

function scrollToBottom() {
  transcript.scrollTop = transcript.scrollHeight;
}

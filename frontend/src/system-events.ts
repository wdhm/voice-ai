const transcript = document.getElementById('transcript')!;

interface ToolMeta {
  icon: string;
  label: string;
  describeCalling: (args: string) => string;
  describeResult: (result: any) => string;
}

const toolMeta: Record<string, ToolMeta> = {
  search_knowledge_base: {
    icon: '🔍',
    label: 'Knowledge Retrieval',
    describeCalling: (args) => {
      try {
        const parsed = JSON.parse(args);
        return `Searching policy documents for "${parsed.query}"...`;
      } catch { return 'Searching knowledge base...'; }
    },
    describeResult: (result) => {
      if (!result.found) return 'No matching documents found.';
      const results = result.results ?? [];
      if (results.length > 0) {
        const sources = results.map((r: any) =>
          (r.source ?? '').replace('atlas-', '').replace(/-/g, ' ')
        );
        return `✓ Found ${results.length} source(s): ${sources.join(', ')}`;
      }
      return '✓ Relevant policy content retrieved';
    },
  },
  authenticate_customer: {
    icon: '🔐',
    label: 'Authentication',
    describeCalling: (args) => {
      try {
        const parsed = JSON.parse(args);
        return `Verifying booking reference ${parsed.booking_reference}...`;
      } catch { return 'Authenticating customer...'; }
    },
    describeResult: (result) => {
      if (result.authenticated) return `✓ Authenticated — ${result.passenger_name}`;
      return '✗ Booking reference not found';
    },
  },
  get_booking_details: {
    icon: '📋',
    label: 'Data Retrieval',
    describeCalling: (args) => {
      try {
        const parsed = JSON.parse(args);
        return `Fetching booking ${parsed.booking_reference}...`;
      } catch { return 'Retrieving booking details...'; }
    },
    describeResult: (result) => {
      if (result.found === false) return 'Booking not found.';
      return `✓ ${result.route} — ${result.flight_number} on ${result.departure_date}`;
    },
  },
  get_baggage_options: {
    icon: '🧳',
    label: 'Data Retrieval',
    describeCalling: () => 'Loading available baggage options...',
    describeResult: (result) => {
      if (!result.available_options) return 'No options available.';
      return `✓ ${result.available_options.length} baggage option(s) available`;
    },
  },
  add_baggage_to_booking: {
    icon: '✅',
    label: 'Transaction',
    describeCalling: (args) => {
      try {
        const parsed = JSON.parse(args);
        return `Adding ${parsed.baggage_type?.replace(/_/g, ' ')} to ${parsed.booking_reference}...`;
      } catch { return 'Processing baggage addition...'; }
    },
    describeResult: (result) => {
      if (result.success) return `✓ Confirmed — ${result.confirmation_number} (${result.price} ${result.currency}) 💾`;
      return `✗ Failed: ${result.message}`;
    },
  },
  remove_baggage_from_booking: {
    icon: '🗑️',
    label: 'Transaction',
    describeCalling: (args) => {
      try {
        const parsed = JSON.parse(args);
        return `Removing ${parsed.baggage_type?.replace(/_/g, ' ')} from ${parsed.booking_reference}...`;
      } catch { return 'Processing baggage removal...'; }
    },
    describeResult: (result) => {
      if (result.success) return '✓ Baggage removed — booking updated 💾';
      return `✗ Failed: ${result.message}`;
    },
  },
  send_confirmation_email: {
    icon: '📧',
    label: 'Email',
    describeCalling: (args) => {
      try {
        const parsed = JSON.parse(args);
        return `Sending confirmation email for ${parsed.booking_reference}...`;
      } catch { return 'Sending confirmation email...'; }
    },
    describeResult: (result) => {
      if (result.success) return `✓ Email sent to ${result.email} 💾`;
      return `✗ Failed: ${result.message}`;
    },
  },
  escalate_to_agent: {
    icon: '🔄',
    label: 'Escalation',
    describeCalling: (args) => {
      try {
        const parsed = JSON.parse(args);
        return `Transferring to agent — ${parsed.reason}`;
      } catch { return 'Initiating handoff to human agent...'; }
    },
    describeResult: (result) => {
      const ref = result.reference_number ?? '';
      const summary = result.conversation_summary ?? '';
      return `✓ Escalated (${ref})${summary ? `\nContext: ${summary}` : ''}`;
    },
  },
};

const defaultMeta: ToolMeta = {
  icon: '⚙️',
  label: 'System',
  describeCalling: () => 'Processing...',
  describeResult: () => 'Done.',
};

let currentCard: HTMLDivElement | null = null;

export function showFunctionCalling(name: string, args: string) {
  const meta = toolMeta[name] ?? defaultMeta;

  const card = document.createElement('div');
  card.className = 'sys-card';
  card.innerHTML = `
    <div class="sys-header">
      <span class="sys-icon">${meta.icon}</span>
      <span class="sys-label">${meta.label}</span>
      <span class="sys-dots"><span>.</span><span>.</span><span>.</span></span>
    </div>
    <div class="sys-detail sys-calling">${meta.describeCalling(args)}</div>
    <div class="sys-detail sys-result" style="display:none"></div>
  `;

  transcript.appendChild(card);
  transcript.scrollTop = transcript.scrollHeight;
  currentCard = card;
}

export function showFunctionResult(name: string, result: unknown) {
  const meta = toolMeta[name] ?? defaultMeta;
  const description = meta.describeResult(result);

  // Update current card if it matches, otherwise create standalone
  const card = currentCard ?? document.createElement('div');
  if (!currentCard) {
    card.className = 'sys-card';
    card.innerHTML = `
      <div class="sys-header">
        <span class="sys-icon">${meta.icon}</span>
        <span class="sys-label">${meta.label}</span>
      </div>
      <div class="sys-detail sys-result"></div>
    `;
    transcript.appendChild(card);
  }

  // Hide loading dots, show result
  const dots = card.querySelector('.sys-dots');
  if (dots) (dots as HTMLElement).style.display = 'none';

  const callingEl = card.querySelector('.sys-calling');
  if (callingEl) (callingEl as HTMLElement).style.opacity = '0.6';

  const resultEl = card.querySelector('.sys-result')!;
  (resultEl as HTMLElement).style.display = 'block';
  resultEl.textContent = description;

  transcript.scrollTop = transcript.scrollHeight;
  currentCard = null;
}

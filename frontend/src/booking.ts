interface BookingData {
  reference: string;
  passengerName: string;
  email: string;
  route: string;
  departureDate: string;
  flightNumber: string;
  ticketClass: string;
  includedBaggage: string;
  extraBaggage: string[];
  confirmationEmailSent: boolean;
  status: string;
}

const panel = document.getElementById('booking-panel')!;
let isVisible = false;

export function hideBooking() {
  if (!isVisible) return;
  panel.classList.remove('visible');
  document.getElementById('app')!.classList.remove('with-booking');
  panel.innerHTML = '';
  isVisible = false;
}

export function showBooking(booking: BookingData) {
  const [origin, destination] = booking.route.split('→').map(s => s.trim());
  const depDate = formatDate(booking.departureDate);

  panel.innerHTML = `
    <div class="bp-header">
      <div class="bp-title">Booking Details</div>
      <div class="bp-ref">${booking.reference}</div>
    </div>

    <div class="bp-route">
      <div class="bp-airport">
        <span class="bp-code">${origin}</span>
      </div>
      <div class="bp-arrow">
        <svg width="32" height="16" viewBox="0 0 32 16"><path d="M0 8h28m0 0l-6-6m6 6l-6 6" stroke="currentColor" stroke-width="1.5" fill="none"/></svg>
      </div>
      <div class="bp-airport">
        <span class="bp-code">${destination}</span>
      </div>
    </div>

    <div class="bp-details">
      <div class="bp-row">
        <span class="bp-label">Passenger</span>
        <span class="bp-value">${booking.passengerName}</span>
      </div>
      <div class="bp-row">
        <span class="bp-label">Flight</span>
        <span class="bp-value">${booking.flightNumber}</span>
      </div>
      <div class="bp-row">
        <span class="bp-label">Date</span>
        <span class="bp-value">${depDate}</span>
      </div>
      <div class="bp-row">
        <span class="bp-label">Class</span>
        <span class="bp-value">${booking.ticketClass}</span>
      </div>
      <div class="bp-row">
        <span class="bp-label">Status</span>
        <span class="bp-value bp-status">${booking.status}</span>
      </div>
    </div>

    <div class="bp-section">
      <div class="bp-section-title">🧳 Baggage</div>
      <div class="bp-baggage-included">${booking.includedBaggage}</div>
      ${booking.extraBaggage.length > 0 ? `
        <div class="bp-extras">
          ${booking.extraBaggage.map(b => `<div class="bp-extra-item">+ ${formatBaggageType(b)}</div>`).join('')}
        </div>
      ` : ''}
    </div>

    <div class="bp-section">
      <div class="bp-section-title">📧 Confirmation</div>
      <div class="bp-confirmation ${booking.confirmationEmailSent ? 'sent' : 'pending'}">
        ${booking.confirmationEmailSent ? '✓ Email sent' : '⏳ Pending'}
      </div>
    </div>
  `;

  if (!isVisible) {
    panel.classList.add('visible');
    document.getElementById('app')!.classList.add('with-booking');
    isVisible = true;
  } else {
    // Flash the panel briefly to indicate an update
    panel.classList.add('updated');
    setTimeout(() => panel.classList.remove('updated'), 600);
  }
}

function formatDate(iso: string): string {
  const d = new Date(iso + 'T00:00:00');
  return d.toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' });
}

function formatBaggageType(type: string): string {
  const map: Record<string, string> = {
    extra_checked_23kg: 'Checked bag (23 kg)',
    extra_checked_32kg: 'Checked bag (32 kg)',
    overweight_23_to_32: 'Overweight upgrade',
    sports_equipment: 'Sports equipment',
    musical_instrument: 'Musical instrument',
  };
  return map[type] ?? type.replace(/_/g, ' ');
}

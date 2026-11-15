const isAndroid = () => /Android/i.test(navigator.userAgent || '');
const isIOS = () => /iPad|iPhone|iPod/i.test(navigator.userAgent || '');

export const isMobileBrowser = () => isAndroid() || isIOS();

// Native WebViews receive a complete document, including the selected paper width.
export const buildNativePrintDocument = ({ pageSize, bodyWidth, bodyPadding = '0', html = '' }) => `<!doctype html>
<html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<style>
@page { size: ${pageSize}; margin: 0; }
* { box-sizing: border-box; }
html, body { margin: 0; background: white; }
body { width: ${bodyWidth}; padding: ${bodyPadding}; font-family: Pyidaungsu, sans-serif;
  -webkit-print-color-adjust: exact; print-color-adjust: exact; }
table { border-collapse: collapse; }
</style></head><body>${html}</body></html>`;

/**
 * Opens a print-friendly window and triggers printing.
 * On mobile (Android/iOS), printing often requires a user gesture and auto-closing
 * can cancel the system preview; we render a "Tap to Print" button instead.
 */
export const openPrintWindow = ({
  title = 'Print',
  pageSize = 'auto',
  bodyWidth = 'auto',
  bodyPadding = '0',
  html = '',
  extraCss = '',
  autoPrint = !isMobileBrowser()
} = {}) => {
  const printWindow = window.open('', '_blank', 'width=900,height=800');
  if (!printWindow) return { ok: false, reason: 'popup_blocked' };

  const showManualControls = isMobileBrowser() || !autoPrint;

  const controlsHtml = showManualControls
    ? `
      <div id="vw-print-controls" style="position: sticky; top: 0; z-index: 9999; padding: 10px; background: #fff; border-bottom: 1px solid #e5e7eb; display: flex; gap: 10px; align-items: center;">
        <button id="vw-print-btn" style="padding: 10px 14px; border-radius: 10px; border: 1px solid #111827; background: #111827; color: white; font-family: system-ui, -apple-system, Segoe UI, Roboto, Arial; font-size: 14px;">Print</button>
        <button id="vw-close-btn" style="padding: 10px 14px; border-radius: 10px; border: 1px solid #d1d5db; background: #fff; color: #111827; font-family: system-ui, -apple-system, Segoe UI, Roboto, Arial; font-size: 14px;">Close</button>
        <div style="margin-left: auto; color: #6b7280; font-family: system-ui, -apple-system, Segoe UI, Roboto, Arial; font-size: 12px;">
          If you don’t see printers, enable a Print Service in Android settings.
        </div>
      </div>
    `
    : '';

  printWindow.document.write(`
    <html>
      <head>
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <title>${title}</title>
        <style>
          @font-face { font-family: 'Pyidaungsu'; src: local('Pyidaungsu'); }
          @page { size: ${pageSize}; margin: 0; }
          body {
            margin: 0;
            padding: ${bodyPadding};
            width: ${bodyWidth};
            font-family: 'Pyidaungsu', sans-serif;
            -webkit-print-color-adjust: exact !important;
            print-color-adjust: exact !important;
            background: white;
          }
          * { box-sizing: border-box; }
          body > :not(#vw-print-controls), body > :not(#vw-print-controls) * { color: black !important; }
          #vw-print-controls, #vw-print-controls * { color: inherit !important; }
          #vw-print-btn { color: white !important; }
          #vw-close-btn { color: #111827 !important; }
          table { border-collapse: collapse; }
          @media print {
            #vw-print-controls { display: none !important; }
          }
          ${extraCss || ''}
        </style>
      </head>
      <body>
        ${controlsHtml}
        ${html || ''}
        <script>
          (function () {
            var autoPrint = ${autoPrint ? 'true' : 'false'};
            var showManual = ${showManualControls ? 'true' : 'false'};
            var printBtn = document.getElementById('vw-print-btn');
            var closeBtn = document.getElementById('vw-close-btn');

            if (printBtn) printBtn.addEventListener('click', function () { window.print(); });
            if (closeBtn) closeBtn.addEventListener('click', function () { window.close(); });

            // Desktop browsers can auto-print and close after print reliably.
            if (!showManual) {
              window.addEventListener('afterprint', function () { window.close(); });
            }

            // On some browsers, printing too early results in blank preview; delay slightly.
            window.onload = function () {
              if (autoPrint && !showManual) setTimeout(function () { window.print(); }, 200);
            };
          })();
        <\/script>
      </body>
    </html>
  `);

  printWindow.document.close();
  return { ok: true };
};

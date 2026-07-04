import html2canvas from 'html2canvas';

/**
 * Takes a screenshot of a DOM element and triggers the share sheet
 * (Web Share API on mobile, download fallback on desktop).
 */
export function useShareInvoice() {
  const isMobile = () => /Android|iPhone|iPad|iPod/i.test(navigator.userAgent || '');

  /**
   * Capture a DOM element as a PNG blob.
   * @param {string} elementId - The ID of the element to screenshot (e.g. 'print-area', 'print-area-a4')
   * @param {object} options
   * @param {number} [options.scale=2] - Device pixel ratio for sharp output
   * @param {string} [options.backgroundColor='#ffffff']
   * @returns {Promise<Blob|null>}
   */
  const captureElement = async (elementId, options = {}) => {
    const el = document.getElementById(elementId);
    if (!el) {
      console.error(`[ShareInvoice] Element #${elementId} not found.`);
      return null;
    }

    // Temporarily make the element visible off-screen for capture
    const originalDisplay = el.style.display;
    const originalPosition = el.style.position;
    const originalLeft = el.style.left;
    const originalTop = el.style.top;
    const originalZIndex = el.style.zIndex;

    el.style.display = 'block';
    el.style.position = 'fixed';
    el.style.left = '-9999px';
    el.style.top = '0';
    el.style.zIndex = '-1';

    try {
      const canvas = await html2canvas(el, {
        scale: options.scale || 2,
        backgroundColor: options.backgroundColor || '#ffffff',
        useCORS: true,
        allowTaint: true,
        logging: false,
      });

      return new Promise((resolve) => {
        canvas.toBlob((blob) => resolve(blob), 'image/png', 1.0);
      });
    } finally {
      // Restore original styles
      el.style.display = originalDisplay;
      el.style.position = originalPosition;
      el.style.left = originalLeft;
      el.style.top = originalTop;
      el.style.zIndex = originalZIndex;
    }
  };

  /**
   * Share an invoice slip as a screenshot image.
   * On mobile: uses Web Share API (supports Facebook, Viber, Messenger, etc.)
   * On desktop: downloads the image and shows a toast with instructions.
   *
   * @param {string} elementId - The DOM element ID to capture
   * @param {object} options
   * @param {string} [options.title='Invoice'] - Share title
   * @param {string} [options.text=''] - Share description text
   * @param {string} [options.fileName='invoice.png'] - Fallback download filename
   * @param {Function} [options.onToast] - Toast callback: (message, type) => void
   * @returns {Promise<boolean>} Whether the share was initiated
   */
  const shareInvoice = async (elementId, options = {}) => {
    const {
      title = 'Invoice',
      text = '',
      fileName = 'invoice.png',
      onToast = null,
    } = options;

    const blob = await captureElement(elementId, { scale: 2 });
    if (!blob) {
      if (onToast) onToast('Could not capture invoice image.', 'error');
      return false;
    }

    const file = new File([blob], fileName, { type: 'image/png' });

    // Try Web Share API first (works on mobile: Android Chrome, iOS Safari)
    if (navigator.share && navigator.canShare && navigator.canShare({ files: [file] })) {
      try {
        await navigator.share({
          files: [file],
          title: title,
          text: text,
        });
        return true;
      } catch (err) {
        // User cancelled or share failed — fall through to download
        if (err.name !== 'AbortError') {
          console.warn('[ShareInvoice] Web Share failed:', err);
        }
      }
    }

    // Fallback: download the image (desktop browsers)
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);

    if (onToast) {
      onToast(
        isMobile()
          ? 'Invoice image downloaded. Share it via your messaging apps.'
          : 'Invoice image downloaded. You can share it via messaging apps.',
        'info'
      );
    }
    return true;
  };

  return { captureElement, shareInvoice };
}

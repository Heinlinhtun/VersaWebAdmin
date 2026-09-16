// Epson TM Print Assistant URL-scheme integration. The native app owns printer setup.
export const isAppleMobile = (nav = navigator) =>
  /iPad|iPhone|iPod/i.test(nav.userAgent || '') ||
  (/Macintosh/i.test(nav.userAgent || '') && nav.maxTouchPoints > 1);

export const buildEpsonUrl = (xml) => {
  const url = 'tmprintassistant://tmprintassistant.epson.com/print?ver=1' +
    '&data-type=eposprintxml&error-dialog=yes&reselect=yes&data=' + encodeURIComponent(xml);
  // Conservative handoff limit; never silently truncate a receipt.
  if (url.length > 180000) throw new Error('EPSON_RECEIPT_TOO_LARGE');
  return url;
};

export const buildRasterXml = (rgba, width, height) => {
  if (!Number.isInteger(width) || width < 8 || width % 8 ||
      !Number.isInteger(height) || height < 1 || rgba.length !== width * height * 4) {
    throw new Error('Invalid receipt image');
  }
  const stride = width / 8;
  let images = '';
  for (let top = 0; top < height; top += 512) {
    const rows = Math.min(512, height - top);
    const bytes = new Uint8Array(stride * rows);
    for (let y = 0; y < rows; y++) {
      for (let x = 0; x < width; x++) {
        const i = ((top + y) * width + x) * 4;
        const alpha = rgba[i + 3] / 255;
        const luminance = (0.299 * rgba[i] + 0.587 * rgba[i + 1] + 0.114 * rgba[i + 2]) * alpha + 255 * (1 - alpha);
        if (luminance < 180) bytes[y * stride + (x >> 3)] |= 0x80 >> (x & 7);
      }
    }
    let binary = '';
    for (const byte of bytes) binary += String.fromCharCode(byte);
    images += `<image width="${width}" height="${rows}" color="color_1" mode="mono">${btoa(binary)}</image>`;
  }
  return '<epos-print xmlns="http://www.epson-pos.com/schemas/2011/03/epos-print">' +
    images + '<feed line="3"/><cut type="feed"/></epos-print>';
};

export const prepareEpsonReceipt = async (blob) => {
  const objectUrl = URL.createObjectURL(blob);
  try {
    const image = new Image();
    await new Promise((resolve, reject) => {
      image.onload = resolve;
      image.onerror = () => reject(new Error('Cannot load receipt image'));
      image.src = objectUrl;
    });
    const width = 576; // TM-m30III, 80mm paper: 72mm printable at 203 dpi.
    const height = Math.max(1, Math.round(image.naturalHeight * width / image.naturalWidth));
    if (height > 1600) throw new Error('EPSON_RECEIPT_TOO_LARGE');
    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext('2d');
    context.fillStyle = '#fff';
    context.fillRect(0, 0, width, height);
    context.drawImage(image, 0, 0, width, height);
    return buildEpsonUrl(buildRasterXml(context.getImageData(0, 0, width, height).data, width, height));
  } finally {
    URL.revokeObjectURL(objectUrl);
  }
};

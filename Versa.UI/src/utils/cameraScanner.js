export function cameraErrorKey(error) {
  if (['NotAllowedError', 'PermissionDeniedError', 'SecurityError'].includes(error?.name)) return 'permissionDenied';
  if (['NotFoundError', 'DevicesNotFoundError', 'OverconstrainedError'].includes(error?.name)) return 'unavailable';
  if (['NotReadableError', 'TrackStartError', 'AbortError'].includes(error?.name)) return 'busy';
  return 'failed';
}

async function createDecoder() {
  const [{ BrowserMultiFormatReader }, { NotFoundException, ChecksumException, FormatException }] = await Promise.all([
    import('@zxing/browser'), import('@zxing/library'),
  ]);
  return {
    reader: new BrowserMultiFormatReader(undefined, { delayBetweenScanAttempts: 150 }),
    isExpectedError: error => error instanceof NotFoundException || error instanceof ChecksumException || error instanceof FormatException,
  };
}

// Own the stream so cancellation also covers permission and video playback promises.
export function createCameraScanner({ video, onResult, onError, onReady = () => {},
  mediaDevices = globalThis.navigator?.mediaDevices, secureContext = globalThis.isSecureContext,
  loadDecoder = createDecoder }) {
  let current;
  function release(session) {
    session.controls?.stop();
    session.stream?.getTracks().forEach(track => track.stop());
    if (session.stream && video.srcObject === session.stream) {
      video.pause();
      video.srcObject = null;
    }
  }
  function stop() {
    const session = current;
    current = undefined;
    if (session) release(session);
  }
  async function start() {
    stop();
    const session = {};
    current = session;
    const active = () => current === session;
    const fail = key => {
      if (!active()) return;
      stop();
      onError(key);
    };
    if (!secureContext) { fail('insecure'); return; }
    if (!mediaDevices?.getUserMedia) { fail('unsupported'); return; }
    try {
      const { reader, isExpectedError } = await loadDecoder();
      if (!active()) return;
      session.stream = await mediaDevices.getUserMedia({ audio: false, video: { facingMode: { ideal: 'environment' } } });
      if (!active()) { release(session); return; }
      video.srcObject = session.stream;
      await video.play();
      if (!active()) { release(session); return; }
      onReady();
      // scan() is synchronous: no decoder can start later after the modal closes.
      const controls = reader.scan(video, (result, error, callbackControls) => {
        if (!active()) return;
        if (result && result.getText().trim()) {
          session.controls = callbackControls;
          const text = result.getText();
          stop();
          onResult(text);
        } else if (error && !isExpectedError(error)) {
          session.controls = callbackControls;
          fail('failed');
        }
      });
      if (active()) session.controls = controls;
      else controls.stop();
    } catch (error) {
      if (active()) fail(cameraErrorKey(error));
      else release(session);
    }
  }
  return { start, stop };
}

import { prepareZXingModule } from '@yudiel/react-qr-scanner'

let readyPromise = null
try {
  readyPromise = prepareZXingModule({
    overrides: {
      locateFile: (path, prefix) => (
        path === 'zxing_reader.wasm'
          ? `${import.meta.env.BASE_URL}qr-scanner/zxing_reader.wasm`
          : `${prefix}${path}`
      ),
    },
  })
  if (readyPromise && typeof readyPromise.catch === 'function') {
    readyPromise.catch((err) => {
      console.warn('[ZXing] Preload warning:', err)
    })
  }
} catch (err) {
  console.warn('[ZXing] Failed to initialize module:', err)
}

export const zxingModuleReady = readyPromise

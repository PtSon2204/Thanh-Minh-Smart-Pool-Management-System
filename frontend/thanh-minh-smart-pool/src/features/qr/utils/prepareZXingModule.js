import { prepareZXingModule } from '@yudiel/react-qr-scanner'

export const zxingModuleReady = prepareZXingModule({
  overrides: {
    locateFile: (path, prefix) => (
      path === 'zxing_reader.wasm'
        ? `${import.meta.env.BASE_URL}qr-scanner/zxing_reader.wasm`
        : `${prefix}${path}`
    ),
  },
})

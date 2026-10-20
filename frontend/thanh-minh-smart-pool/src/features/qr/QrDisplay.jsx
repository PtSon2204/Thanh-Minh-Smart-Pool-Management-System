import { QRCodeSVG } from 'qrcode.react'

export function QrDisplay({ payload, ...props }) {
  return <QRCodeSVG {...props} value={payload} />
}

export default QrDisplay

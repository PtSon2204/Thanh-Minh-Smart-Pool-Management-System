import { useState } from 'react'
import { Button, Space, message } from 'antd'
import { useQueryClient } from '@tanstack/react-query'
import { monitoringService } from '../services/monitoringService'
import { canHandleOpenAlert } from '../utils/alertActionWindow'

// The popup and its live camera dialog use the same guarded ACK/CANCEL actions.
export default function AlertActionButtons({ alert, onHandled }) {
  const [busy, setBusy] = useState(false)
  const client = useQueryClient()
  if (!canHandleOpenAlert(alert)) return null
  const handle = async action => {
    setBusy(true)
    try {
      const handled = await monitoringService.handleAlert(alert.id, action, alert.severity)
      void client.invalidateQueries({ queryKey: ['safety'] })
      message.success(action === 'cancel' ? 'Đã hủy cảnh báo.' : handled.incidentId
        ? 'Đã xác nhận và lưu vào lịch sử sự cố.' : 'Đã xác nhận cảnh báo.')
      onHandled?.(handled)
    } catch (error) {
      void client.invalidateQueries({ queryKey: ['safety'] })
      message.error(error.response?.data?.message || 'Không xử lý được cảnh báo. Vui lòng thử lại.')
    } finally { setBusy(false) }
  }
  return <Space wrap>
    <Button size="small" loading={busy} disabled={busy} onClick={() => handle('acknowledge')}>Xác nhận</Button>
    <Button size="small" danger disabled={busy} onClick={() => handle('cancel')}>Hủy</Button>
  </Space>
}

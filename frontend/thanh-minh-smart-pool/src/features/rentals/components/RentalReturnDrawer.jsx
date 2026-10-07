import { Alert, Button, Drawer, InputNumber, Spin, Typography } from 'antd'
import { useState } from 'react'
import { getRentalErrorMessage, useReturnRentalQuantity } from '../hooks/useRentalMutations'

export default function RentalReturnDrawer({ open, details, detailsError, detailsLoading, onClose, onConflict, onRetryDetails }) {
  const [quantity, setQuantity] = useState(1)
  const [frozenRentalIds, setFrozenRentalIds] = useState(null)
  const [selectionRefreshed, setSelectionRefreshed] = useState(false)
  const mutation = useReturnRentalQuantity()
  const maximum = Math.min(details?.outstandingQuantity || 0, 100)
  const selectedQuantity = frozenRentalIds?.length || quantity
  const canConfirm = maximum > 0 && Number.isInteger(selectedQuantity) && selectedQuantity >= 1 && selectedQuantity <= maximum

  const handleSubmit = () => {
    if (!canConfirm || mutation.isPending || !details) return
    setSelectionRefreshed(false)
    const rentalIds = frozenRentalIds || (details.outstandingRentalIds || []).slice(0, selectedQuantity)
    if (rentalIds.length !== selectedQuantity) return
    setFrozenRentalIds(rentalIds)
    mutation.mutate(
      { orderId: details.orderId, productId: details.productId, rentalIds },
      {
        onSuccess: onClose,
        onError: async (error) => {
          if (error?.response?.status === 409) {
            const refreshed = await onConflict()
            if (refreshed?.data) {
              setFrozenRentalIds(null)
              setQuantity(1)
              setSelectionRefreshed(true)
              mutation.reset()
            }
          }
        },
      },
    )
  }

  const close = () => {
    if (!mutation.isPending) onClose()
  }

  return <Drawer rootClassName="rental-return-drawer" title="Nhận lại sản phẩm thuê" open={open} onClose={close} closable={!mutation.isPending} mask={{ closable: !mutation.isPending }} push={false} size="default" styles={{ body: { overflowY: 'auto' } }}>
    {detailsLoading && <div className="admin-management-state"><Spin size="large" /></div>}
    {!detailsLoading && detailsError && <Alert type="error" showIcon message="Không thể tải lựa chọn trả hàng" description={getRentalErrorMessage(detailsError, 'Không thể tải chi tiết đơn thuê.')} action={<Button size="small" onClick={onRetryDetails}>Thử lại</Button>} />}
    {!detailsLoading && !detailsError && !details && <Alert type="warning" showIcon message="Không tìm thấy chi tiết đơn thuê để nhận lại." />}
    {!detailsLoading && !detailsError && details && <div className="rental-return-stack">
      <Typography.Paragraph>Chọn số lượng cần nhận lại. Hệ thống chỉ xử lý tối đa 100 đơn vị mỗi lần.</Typography.Paragraph>
      {mutation.error && <Alert type="error" showIcon message="Không thể nhận lại sản phẩm" description={getRentalErrorMessage(mutation.error, 'Yêu cầu chưa xác định kết quả. Bạn có thể thử lại với đúng số lượng đã chọn.')} />}
      {selectionRefreshed && <Alert type="info" showIcon message="Dữ liệu đã được cập nhật" description="Hãy chọn lại số lượng và xác nhận một yêu cầu mới." />}
      {frozenRentalIds && <Alert type="info" showIcon message="Đã giữ nguyên lựa chọn trả hàng" description="Lần thử lại sẽ dùng đúng tập bản ghi đã chọn. Đóng biểu mẫu để chọn lại." />}
      <label className="rental-return-field"><span>Số lượng nhận lại</span><InputNumber min={1} max={maximum} precision={0} value={selectedQuantity} disabled={mutation.isPending || Boolean(frozenRentalIds)} onChange={(value) => setQuantity(value || 0)} /></label>
      <div className="rental-quantity-summary" aria-label="Xem trước số lượng sau khi nhận lại">
        <span><strong>{details.outstandingQuantity}</strong> còn lại trước khi nhận</span>
        <span><strong>{selectedQuantity}</strong> sẽ nhận lại</span>
        <span><strong>{Math.max(0, details.outstandingQuantity - selectedQuantity)}</strong> còn lại sau khi nhận</span>
        <span><strong>+{selectedQuantity}</strong> tăng tồn kho dự kiến</span>
      </div>
      <Button type="primary" loading={mutation.isPending} disabled={!canConfirm || mutation.isPending} onClick={handleSubmit}>{frozenRentalIds ? 'Thử lại yêu cầu' : 'Xác nhận nhận lại'}</Button>
    </div>}
  </Drawer>
}

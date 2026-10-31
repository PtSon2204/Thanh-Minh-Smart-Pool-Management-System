import { Alert, Button, Checkbox, Drawer, Form, Input, InputNumber, Spin, Typography } from 'antd'
import { useEffect, useMemo, useRef, useState } from 'react'
import { getRentalErrorMessage, useReturnRentalQuantity } from '../hooks/useRentalMutations'
import { createRentalRequestId, isRentalRequestOutcomeUncertain } from '../utils/rentalRequests'

const DEPOSIT_PER_UNIT = 20000
const currencyFormatter = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })

function freezeReturnPayload(payload) {
  return Object.freeze({
    ...payload,
    rentalIds: Object.freeze([...payload.rentalIds]),
    damagedRentalIds: Object.freeze([...payload.damagedRentalIds]),
    lostRentalIds: Object.freeze([...payload.lostRentalIds]),
  })
}

export default function RentalReturnDrawer({ open, details, detailsError, detailsLoading, onClose, onConflict, onRetryDetails }) {
  const [form] = Form.useForm()
  const [frozenReturn, setFrozenReturn] = useState(null)
  const [selectionRefreshed, setSelectionRefreshed] = useState(false)
  const submitting = useRef(false)
  const mutation = useReturnRentalQuantity()
  const resetReturnMutation = mutation.reset
  const normalQuantity = Form.useWatch('normalQuantity', form) || 0
  const damagedQuantity = Form.useWatch('damagedQuantity', form) || 0
  const lostQuantity = Form.useWatch('lostQuantity', form) || 0
  const selectedQuantity = normalQuantity + damagedQuantity + lostQuantity
  const maximum = Math.min(details?.outstandingQuantity || 0, 100)
  const isLegacy = details?.collectionVerified === false
  const requiresReason = damagedQuantity + lostQuantity > 0
  const isLocked = Boolean(frozenReturn)
  const outstandingRentals = useMemo(() => (details?.rentals || []).filter((record) => record.status === 'Renting'), [details?.rentals])
  const availableIds = details?.outstandingRentalIds || outstandingRentals.map((record) => record.id)
  const depositByRentalId = useMemo(() => new Map(outstandingRentals.map((record) => [record.id, record.depositAmount ?? DEPOSIT_PER_UNIT])), [outstandingRentals])
  const selectedIds = isLocked ? {
    normal: frozenReturn.rentalIds,
    damaged: frozenReturn.damagedRentalIds,
    lost: frozenReturn.lostRentalIds,
  } : {
    normal: availableIds.slice(0, normalQuantity),
    damaged: availableIds.slice(normalQuantity, normalQuantity + damagedQuantity),
    lost: availableIds.slice(normalQuantity + damagedQuantity, selectedQuantity),
  }
  const refundAmount = selectedIds.normal.reduce((total, id) => total + (depositByRentalId.get(id) ?? DEPOSIT_PER_UNIT), 0)
  const retainedAmount = [...selectedIds.damaged, ...selectedIds.lost].reduce((total, id) => total + (depositByRentalId.get(id) ?? DEPOSIT_PER_UNIT), 0)
  const canConfirm = isLocked || (!isLegacy && maximum > 0 && selectedQuantity >= 1 && selectedQuantity <= maximum && selectedIds.normal.length + selectedIds.damaged.length + selectedIds.lost.length === selectedQuantity)

  useEffect(() => {
    if (!open || !details || frozenReturn) return
    form.resetFields()
    form.setFieldsValue({ normalQuantity: Math.min(1, maximum), damagedQuantity: 0, lostQuantity: 0, settlementConfirmed: false })
    resetReturnMutation()
  }, [details, form, frozenReturn, maximum, open, resetReturnMutation])

  const handleSubmit = async () => {
    if (!canConfirm || mutation.isPending || submitting.current || !details) return
    submitting.current = true
    let payload = frozenReturn

    if (!payload) {
      try {
        const values = await form.validateFields()
        payload = freezeReturnPayload({
          requestId: createRentalRequestId(),
          rentalIds: selectedIds.normal,
          damagedRentalIds: selectedIds.damaged,
          lostRentalIds: selectedIds.lost,
          settlementConfirmed: true,
          reason: values.reason?.trim() || null,
        })
        setFrozenReturn(payload)
      } catch {
        submitting.current = false
        return null
      }
    }

    resetReturnMutation()
    mutation.mutate(
      { orderId: details.orderId, productId: details.productId, ...payload },
      {
        onSuccess: onClose,
        onSettled: () => { submitting.current = false },
        onError: async (error) => {
          if (isRentalRequestOutcomeUncertain(error)) return
          setFrozenReturn(null)
          if (error?.response?.status === 409) {
            const refreshed = await onConflict()
            if (refreshed?.data) {
              form.setFieldsValue({ normalQuantity: 1, damagedQuantity: 0, lostQuantity: 0, reason: undefined, settlementConfirmed: false })
              setSelectionRefreshed(true)
            }
          }
        },
      },
    )
  }

  const close = () => {
    if (!mutation.isPending && !isLocked) onClose()
  }

  return <Drawer rootClassName="rental-return-drawer" title="Nhận lại và quyết toán cọc" open={open} onClose={close} closable={!mutation.isPending && !isLocked} mask={{ closable: !mutation.isPending && !isLocked }} push={false} size="default" styles={{ body: { overflowY: 'auto' } }}>
    {detailsLoading && <div className="admin-management-state"><Spin size="large" /></div>}
    {!detailsLoading && detailsError && <Alert type="error" showIcon message="Không thể tải lựa chọn trả hàng" description={getRentalErrorMessage(detailsError, 'Không thể tải chi tiết đơn thuê.')} action={<Button size="small" onClick={onRetryDetails}>Thử lại</Button>} />}
    {!detailsLoading && !detailsError && !details && <Alert type="warning" showIcon message="Không tìm thấy chi tiết đơn thuê để nhận lại." />}
    {!detailsLoading && !detailsError && details && <Form form={form} layout="vertical" preserve={false} className="rental-return-stack" disabled={isLegacy || isLocked} onValuesChange={() => { if (!isLocked && mutation.isError) resetReturnMutation() }}>
      <Typography.Paragraph>Phân loại từng đơn vị. Chỉ đơn vị trả bình thường mới tăng tồn kho. Đơn vị hư hỏng hoặc thất lạc giữ toàn bộ cọc.</Typography.Paragraph>
      {isLegacy && <Alert type="warning" showIcon message="Lịch sử cũ chưa xác minh" description="Lịch sử cũ chưa xác nhận thu cọc. Không thể quyết toán tiền hoặc thay đổi tồn kho từ màn hình này." />}
      {mutation.error && <Alert type="error" showIcon message="Không thể quyết toán trả hàng" description={isLocked ? 'Kết quả quyết toán chưa xác định. Hãy thử lại đúng yêu cầu đã được giữ nguyên.' : getRentalErrorMessage(mutation.error, 'Không thể xử lý trả hàng.')} />}
      {selectionRefreshed && <Alert type="info" showIcon message="Dữ liệu đã được cập nhật" description="Hãy chọn lại các số lượng và xác nhận yêu cầu mới." />}
      {isLocked && <Alert type="info" showIcon message="Đã giữ nguyên yêu cầu quyết toán" description="Lần thử lại sẽ dùng đúng các đơn vị và phân loại đã chọn." />}
      <div className="rental-return-grid">
        <Form.Item label="Trả bình thường" name="normalQuantity" rules={[{ type: 'integer', min: 0, message: 'Nhập số nguyên không âm.' }]}><InputNumber min={0} max={maximum} precision={0} /></Form.Item>
        <Form.Item label="Hư hỏng" name="damagedQuantity" rules={[{ type: 'integer', min: 0, message: 'Nhập số nguyên không âm.' }]}><InputNumber min={0} max={maximum} precision={0} /></Form.Item>
        <Form.Item label="Thất lạc" name="lostQuantity" rules={[{ type: 'integer', min: 0, message: 'Nhập số nguyên không âm.' }]}><InputNumber min={0} max={maximum} precision={0} /></Form.Item>
      </div>
      {requiresReason && <Form.Item label="Lý do hư hỏng hoặc thất lạc" name="reason" rules={[{ required: true, whitespace: true, message: 'Nhập lý do cho đơn vị hư hỏng hoặc thất lạc.' }, { max: 500, message: 'Lý do không vượt quá 500 ký tự.' }]}><Input.TextArea rows={3} /></Form.Item>}
      <div className="rental-quantity-summary" aria-label="Xem trước quyết toán trả hàng">
        <span><strong>{details.outstandingQuantity}</strong> còn lại trước khi xử lý</span>
        <span><strong>{normalQuantity}</strong> trả bình thường, tăng tồn kho</span>
        <span><strong>{damagedQuantity}</strong> hư hỏng, không tăng tồn kho</span>
        <span><strong>{lostQuantity}</strong> thất lạc, không tăng tồn kho</span>
        <span><strong>{currencyFormatter.format(refundAmount)}</strong> hoàn cọc dự kiến</span>
        <span><strong>{currencyFormatter.format(retainedAmount)}</strong> giữ cọc dự kiến</span>
      </div>
      {refundAmount > 0 ? <Form.Item name="cashRefundConfirmed" valuePropName="checked" rules={[{ validator: (_, value) => value ? Promise.resolve() : Promise.reject(new Error('Xác nhận đã hoàn cọc bằng tiền mặt tại quầy.')) }]}>
        <Checkbox>Xác nhận đã hoàn {currencyFormatter.format(refundAmount)} bằng tiền mặt tại quầy.</Checkbox>
      </Form.Item> : <Alert type="info" showIcon message="Không có khoản hoàn cọc bằng tiền mặt" description="Khoản cọc của các đơn vị hư hỏng hoặc thất lạc được giữ lại theo quyết toán." />}
      <Form.Item name="settlementConfirmed" valuePropName="checked" rules={[{ validator: (_, value) => value ? Promise.resolve() : Promise.reject(new Error('Xác nhận hoàn hoặc giữ cọc trước khi xử lý.')) }]}>
        <Checkbox>Xác nhận đã quyết toán cọc đúng theo xem trước.</Checkbox>
      </Form.Item>
      <Button type="primary" loading={mutation.isPending} disabled={!canConfirm || mutation.isPending} onClick={handleSubmit}>{isLocked ? 'Thử lại yêu cầu đã giữ' : 'Xác nhận trả hàng và quyết toán'}</Button>
    </Form>}
  </Drawer>
}

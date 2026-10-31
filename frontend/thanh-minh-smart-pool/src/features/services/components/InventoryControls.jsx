import { useRef, useState } from 'react'
import { ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, DatePicker, Drawer, Empty, Form, Input, InputNumber, Pagination, Radio, Spin, Table, Tag, Typography } from 'antd'
import dayjs from 'dayjs'
import { getServiceErrorMessage, useAdjustStock } from '../hooks/useServiceMutations'
import { useInventoryHistory } from '../hooks/useServices'

function RetryEmpty({ description, onRetry }) {
  return <Empty description={description}><Button icon={<ReloadOutlined />} onClick={onRetry}>Thử lại</Button></Empty>
}

function formatUtcDate(value) {
  if (!value) return '-'
  return `${new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short', timeZone: 'UTC' }).format(new Date(value))} UTC`
}

export default function InventoryControls({ children }) {
  const [selectedService, setSelectedService] = useState(null)
  const [date, setDate] = useState()
  const [historyPageIndex, setHistoryPageIndex] = useState(1)
  const [historyPageSize, setHistoryPageSize] = useState(10)
  const [adjustmentOpen, setAdjustmentOpen] = useState(false)
  const [historyOpen, setHistoryOpen] = useState(false)
  const [form] = Form.useForm()
  const submitting = useRef(false)
  const historyQuery = useInventoryHistory(selectedService?.id, { date, pageIndex: historyPageIndex, pageSize: historyPageSize }, Boolean(selectedService && historyOpen))
  const adjustmentMutation = useAdjustStock()
  const isAdjusting = adjustmentMutation.isPending
  const direction = Form.useWatch('direction', form) || 'increase'
  const quantity = Form.useWatch('quantity', form)
  const currentStock = selectedService?.stockQuantity
  const resultStock = typeof currentStock === 'number' && Number.isInteger(quantity) ? currentStock + (direction === 'increase' ? quantity : -quantity) : currentStock

  const chooseForAdjustment = (service) => {
    setSelectedService(service)
    adjustmentMutation.reset()
    form.resetFields()
    form.setFieldsValue({ direction: 'increase', quantity: undefined, note: undefined })
    setAdjustmentOpen(true)
  }

  const openHistory = (service) => {
    setSelectedService(service)
    setDate(undefined)
    setHistoryPageIndex(1)
    setHistoryOpen(true)
  }

  const handleAdjustmentFinish = (values) => {
    if (submitting.current || isAdjusting || !selectedService) return
    submitting.current = true
    adjustmentMutation.reset()
    adjustmentMutation.mutate(
      { id: selectedService.id, data: { delta: values.direction === 'increase' ? values.quantity : -values.quantity, note: values.note.trim() } },
      {
        onSuccess: (result) => {
          setSelectedService((service) => ({ ...service, stockQuantity: result.stockQuantity }))
          form.resetFields()
          setAdjustmentOpen(false)
        },
        onSettled: () => { submitting.current = false },
      },
    )
  }

  const closeAdjustment = () => {
    if (!isAdjusting) {
      adjustmentMutation.reset()
      form.resetFields()
      setAdjustmentOpen(false)
    }
  }

  const historyColumns = [
    { title: 'Thời gian (UTC)', dataIndex: 'createdAt', key: 'createdAt', render: formatUtcDate },
    { title: 'Thay đổi', dataIndex: 'changeType', key: 'changeType', render: (value) => <Tag color="blue">{{ RentalCheckout: 'Cho thuê', RentalReturn: 'Nhận trả', RentalReservation: 'Giữ hàng chờ chuyển khoản', RentalReservationRelease: 'Hủy giữ hàng', Adjustment: 'Điều chỉnh' }[value] || value}</Tag> },
    { title: 'Số lượng', dataIndex: 'quantity', key: 'quantity', align: 'right', render: (value) => <strong className={value > 0 ? 'inventory-positive' : 'inventory-negative'}>{value > 0 ? `+${value}` : value}</strong> },
    { title: 'Ghi chú', dataIndex: 'note', key: 'note', render: (value) => value || '-' },
    { title: 'Người thực hiện', dataIndex: 'createdBy', key: 'createdBy', render: (value) => value || '-' },
  ]

  return (
    <>
      {children({ onAdjust: chooseForAdjustment, onHistory: openHistory, isAdjusting })}
      <Drawer title={`Lịch sử tồn kho: ${selectedService?.name || ''}`} open={historyOpen} onClose={() => !isAdjusting && setHistoryOpen(false)} closable={!isAdjusting} mask={{ closable: !isAdjusting }} size="large">
        <label className="inventory-history-filter"><span>Ngày theo UTC</span><DatePicker allowClear aria-label="Lọc lịch sử tồn kho theo ngày UTC" value={date ? dayjs(date) : null} disabled={isAdjusting} onChange={(value) => { setDate(value ? value.format('YYYY-MM-DD') : undefined); setHistoryPageIndex(1) }} placeholder="Chọn ngày UTC" /></label>
        {historyQuery.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
        {historyQuery.isError && <RetryEmpty description="Không thể tải lịch sử tồn kho." onRetry={historyQuery.refetch} />}
        {!historyQuery.isLoading && !historyQuery.isError && <Table rowKey="id" columns={historyColumns} dataSource={historyQuery.data?.items || []} pagination={false} scroll={{ x: true }} locale={{ emptyText: 'Chưa có lịch sử điều chỉnh tồn kho.' }} />}
        {!historyQuery.isLoading && !historyQuery.isError && (historyQuery.data?.totalCount || 0) > 0 && <div className="admin-management-pagination"><Pagination current={historyPageIndex} pageSize={historyPageSize} total={historyQuery.data.totalCount} disabled={isAdjusting} showSizeChanger pageSizeOptions={['10', '20', '50', '100']} onChange={(page, size) => { setHistoryPageIndex(page); setHistoryPageSize(size) }} showTotal={(total, range) => `${range[0]}-${range[1]} của ${total} thay đổi`} /></div>}
      </Drawer>
      <Drawer aria-labelledby="inventory-adjustment-title" forceRender open={adjustmentOpen} onClose={closeAdjustment} closable={!isAdjusting} mask={{ closable: !isAdjusting }} size="default">
        <Typography.Title id="inventory-adjustment-title" level={4}>Điều chỉnh tồn kho: {selectedService?.name || ''}</Typography.Title>
        <Form form={form} layout="vertical" onFinish={handleAdjustmentFinish} preserve={false} initialValues={{ direction: 'increase' }}>
          {adjustmentMutation.error && <Alert className="service-inline-error" type="error" showIcon message="Không thể điều chỉnh tồn kho" description={getServiceErrorMessage(adjustmentMutation.error, 'Máy chủ không thể điều chỉnh tồn kho.')} />}
          <Alert type="info" showIcon message="Cập nhật số lượng đồ có thể cho thuê sau khi bổ sung, kiểm kê hoặc ghi nhận mất/hỏng. Lượt thuê và trả đồ được cập nhật tự động." />
          <Form.Item label="Hướng điều chỉnh" name="direction" rules={[{ required: true }]}><Radio.Group disabled={isAdjusting}><Radio.Button value="increase">Tăng do kiểm kê</Radio.Button><Radio.Button value="decrease">Giảm do kiểm kê</Radio.Button></Radio.Group></Form.Item>
          <Form.Item label="Số lượng" name="quantity" rules={[{ required: true, message: 'Nhập số lượng.' }, { validator: (_, value) => Number.isInteger(value) && value > 0 ? Promise.resolve() : Promise.reject(new Error('Số lượng phải là số nguyên dương.')) }, { validator: (_, value) => direction !== 'decrease' || currentStock == null || value <= currentStock ? Promise.resolve() : Promise.reject(new Error('Số lượng xuất không được lớn hơn tồn hiện tại.')) }]}><InputNumber min={1} precision={0} className="service-full-width" disabled={isAdjusting} /></Form.Item>
          <div className="inventory-stock-preview"><span>Tồn hiện tại</span><strong>{currentStock ?? 'Không theo dõi'}</strong><span>Tồn sau điều chỉnh</span><strong>{resultStock ?? 'Không theo dõi'}</strong></div>
          <Form.Item label="Ghi chú" name="note" rules={[{ required: true, whitespace: true, message: 'Nhập ghi chú điều chỉnh.' }]}><Input.TextArea rows={3} showCount disabled={isAdjusting} placeholder="Ví dụ: Bổ sung phao cho thuê hoặc kiểm kê đồ hỏng." /></Form.Item>
          <Button type="primary" htmlType="submit" loading={isAdjusting} disabled={isAdjusting}>Xác nhận</Button>
        </Form>
      </Drawer>
    </>
  )
}

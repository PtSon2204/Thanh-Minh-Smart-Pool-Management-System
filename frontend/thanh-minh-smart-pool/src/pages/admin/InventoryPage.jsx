import { useRef, useState } from 'react'
import { EditOutlined, HistoryOutlined, ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, DatePicker, Drawer, Empty, Form, Input, InputNumber, Pagination, Radio, Select, Spin, Table, Tag, Tooltip, Typography } from 'antd'
import dayjs from 'dayjs'
import { ServiceCategoryTabs, ServiceListHeader, ServiceListToolbar, ServiceNameCell } from '../../features/services/components/ServiceListChrome'
import { getServiceErrorMessage, useAdjustStock } from '../../features/services/hooks/useServiceMutations'
import { useInventoryHistory, useServices } from '../../features/services/hooks/useServices'
import '../../features/services/styles/serviceManagement.css'
import '../../features/shared/styles/adminActions.css'
import '../../features/shared/styles/serviceMobileCards.css'

function RetryEmpty({ description, onRetry }) {
  return <Empty description={description}><Button icon={<ReloadOutlined />} onClick={onRetry}>Thử lại</Button></Empty>
}

function formatUtcDate(value) {
  if (!value) return '-'
  return `${new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short', timeZone: 'UTC' }).format(new Date(value))} UTC`
}

function InventoryMobileCards({ services, disabled, onAdjust, onHistory }) {
  return <div className="inventory-mobile-list">{services.map((service) => {
    const stockTracked = service.stockQuantity != null
    return <article className="inventory-mobile-card" key={service.id}>
      <ServiceNameCell name={service.name} type={service.type} />
      <dl className="service-mobile-card-details">
        <div><dt>Tồn hiện tại</dt><dd className="service-number-cell">{service.stockQuantity ?? 'Không theo dõi'}</dd></div>
        <div><dt>Trạng thái</dt><dd><Tag color={service.isActive ? 'success' : 'error'}>{service.isActive ? 'Hoạt động' : 'Tạm dừng'}</Tag></dd></div>
      </dl>
      <div className="admin-action-group">
        <Tooltip title={stockTracked ? undefined : 'Dịch vụ này không theo dõi tồn kho'}>
          <Button className="admin-action-button admin-action-button--primary" icon={<EditOutlined />} disabled={!stockTracked || disabled} aria-label={`Điều chỉnh tồn kho ${service.name}`} onClick={() => onAdjust(service)}>Điều chỉnh tồn kho</Button>
        </Tooltip>
        <Button className="admin-action-button admin-action-button--view" icon={<HistoryOutlined />} disabled={disabled} aria-label={`Lịch sử tồn kho ${service.name}`} onClick={() => onHistory(service)}>Lịch sử</Button>
      </div>
    </article>
  })}</div>
}

export default function InventoryPage() {
  const [selectedService, setSelectedService] = useState(null)
  const [searchTerm, setSearchTerm] = useState('')
  const [type, setType] = useState()
  const [isActive, setIsActive] = useState()
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [date, setDate] = useState()
  const [historyPageIndex, setHistoryPageIndex] = useState(1)
  const [historyPageSize, setHistoryPageSize] = useState(10)
  const [adjustmentOpen, setAdjustmentOpen] = useState(false)
  const [historyOpen, setHistoryOpen] = useState(false)
  const [form] = Form.useForm()
  const submitting = useRef(false)
  const servicesQuery = useServices({ searchTerm: searchTerm || undefined, type, isActive, pageIndex, pageSize })
  const historyQuery = useInventoryHistory(selectedService?.id, { date, pageIndex: historyPageIndex, pageSize: historyPageSize }, Boolean(selectedService && historyOpen))
  const adjustmentMutation = useAdjustStock()
  const isAdjusting = adjustmentMutation.isPending
  const direction = Form.useWatch('direction', form) || 'increase'
  const quantity = Form.useWatch('quantity', form)
  const currentStock = selectedService?.stockQuantity
  const resultStock = typeof currentStock === 'number' && Number.isInteger(quantity) ? currentStock + (direction === 'increase' ? quantity : -quantity) : currentStock

  const resetPage = (setter) => (value) => {
    setter(value)
    setPageIndex(1)
  }

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

  const serviceColumns = [
    { title: 'Dịch vụ', dataIndex: 'name', key: 'name', render: (name, service) => <ServiceNameCell name={name} type={service.type} /> },
    { title: 'Tồn hiện tại', dataIndex: 'stockQuantity', key: 'stockQuantity', align: 'right', render: (value) => <span className="service-number-cell">{value ?? 'Không theo dõi'}</span> },
    {
      title: 'Thao tác', key: 'action', align: 'right', render: (_, service) => {
        const stockTracked = service.stockQuantity != null
        return <div className="admin-action-group inventory-row-actions"><Tooltip title={stockTracked ? undefined : 'Dịch vụ này không theo dõi tồn kho'}><Button className="admin-action-button admin-action-button--primary" icon={<EditOutlined />} disabled={!stockTracked || isAdjusting} aria-label={`Điều chỉnh tồn kho ${service.name}`} onClick={() => chooseForAdjustment(service)}>Điều chỉnh tồn kho</Button></Tooltip><Button className="admin-action-button admin-action-button--view" icon={<HistoryOutlined />} disabled={isAdjusting} aria-label={`Lịch sử tồn kho ${service.name}`} onClick={() => openHistory(service)}>Lịch sử</Button></div>
      },
    },
  ]
  const historyColumns = [
    { title: 'Thời gian (UTC)', dataIndex: 'createdAt', key: 'createdAt', render: formatUtcDate },
    { title: 'Thay đổi', dataIndex: 'changeType', key: 'changeType', render: (value) => <Tag color="blue">{value}</Tag> },
    { title: 'Số lượng', dataIndex: 'quantity', key: 'quantity', align: 'right', render: (value) => <strong className={value > 0 ? 'inventory-positive' : 'inventory-negative'}>{value > 0 ? `+${value}` : value}</strong> },
    { title: 'Ghi chú', dataIndex: 'note', key: 'note', render: (value) => value || '-' },
    { title: 'Người thực hiện', dataIndex: 'createdBy', key: 'createdBy', render: (value) => value || '-' },
  ]

  return (
    <div className="service-list-page">
      <ServiceListHeader title="Tồn kho" tabs={<ServiceCategoryTabs value={type || 'all'} onChange={(category) => resetPage(setType)(category === 'all' ? undefined : category)} />} />
      <ServiceListToolbar searchTerm={searchTerm} onSearch={resetPage(setSearchTerm)} disabled={isAdjusting} searchLabel="Tìm hàng hóa"><label className="service-filter-control"><span>Trạng thái</span><Select allowClear aria-label="Lọc theo trạng thái dịch vụ" placeholder="Tất cả" disabled={isAdjusting} options={[{ label: 'Hoạt động', value: true }, { label: 'Tạm dừng', value: false }]} value={isActive} onChange={resetPage(setIsActive)} /></label></ServiceListToolbar>
      <section className="service-list-table" aria-label="Danh sách tồn kho">
        {servicesQuery.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
        {servicesQuery.isError && <RetryEmpty description="Không thể tải danh mục tồn kho." onRetry={servicesQuery.refetch} />}
        {!servicesQuery.isLoading && !servicesQuery.isError && <>
          <Table className="inventory-desktop-table" rowKey="id" columns={serviceColumns} dataSource={servicesQuery.data?.items || []} pagination={false} scroll={{ x: true }} locale={{ emptyText: 'Không tìm thấy dịch vụ phù hợp.' }} />
          <InventoryMobileCards services={servicesQuery.data?.items || []} disabled={isAdjusting} onAdjust={chooseForAdjustment} onHistory={openHistory} />
        </>}
        {!servicesQuery.isLoading && !servicesQuery.isError && (servicesQuery.data?.totalCount || 0) > 0 && <div className="admin-management-pagination"><Pagination current={pageIndex} pageSize={pageSize} total={servicesQuery.data.totalCount} disabled={isAdjusting} showSizeChanger pageSizeOptions={['10', '20', '50', '100']} onChange={(page, size) => { setPageIndex(page); setPageSize(size) }} showTotal={(total, range) => `${range[0]}-${range[1]} của ${total} dịch vụ`} /></div>}
      </section>
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
          <Form.Item label="Hướng điều chỉnh" name="direction" rules={[{ required: true }]}><Radio.Group disabled={isAdjusting}><Radio.Button value="increase">Nhập kho</Radio.Button><Radio.Button value="decrease">Xuất kho</Radio.Button></Radio.Group></Form.Item>
          <Form.Item label="Số lượng" name="quantity" rules={[{ required: true, message: 'Nhập số lượng.' }, { validator: (_, value) => Number.isInteger(value) && value > 0 ? Promise.resolve() : Promise.reject(new Error('Số lượng phải là số nguyên dương.')) }, { validator: (_, value) => direction !== 'decrease' || currentStock == null || value <= currentStock ? Promise.resolve() : Promise.reject(new Error('Số lượng xuất không được lớn hơn tồn hiện tại.')) }]}><InputNumber min={1} precision={0} className="service-full-width" disabled={isAdjusting} /></Form.Item>
          <div className="inventory-stock-preview"><span>Tồn hiện tại</span><strong>{currentStock ?? 'Không theo dõi'}</strong><span>Tồn sau điều chỉnh</span><strong>{resultStock ?? 'Không theo dõi'}</strong></div>
          <Form.Item label="Ghi chú" name="note" rules={[{ required: true, whitespace: true, message: 'Nhập ghi chú điều chỉnh.' }]}><Input.TextArea rows={3} showCount disabled={isAdjusting} placeholder="Ví dụ: Nhập thêm phao bơi" /></Form.Item>
          <Button type="primary" htmlType="submit" loading={isAdjusting} disabled={isAdjusting}>Xác nhận</Button>
        </Form>
      </Drawer>
    </div>
  )
}

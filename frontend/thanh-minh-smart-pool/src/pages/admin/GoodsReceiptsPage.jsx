import { useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, DatePicker, Descriptions, Drawer, Empty, Form, Input, InputNumber, message, Popconfirm, Select, Space, Spin, Table, Tag, Typography } from 'antd'
import { DeleteOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { useSearchParams } from 'react-router-dom'
import { goodsReceiptService } from '../../features/services/services/goodsReceiptService'
import { useServices } from '../../features/services/hooks/useServices'
import { getServiceErrorMessage } from '../../features/services/hooks/useServiceMutations'
import { ServiceListHeader } from '../../features/services/components/ServiceListChrome'
import '../../features/services/styles/serviceManagement.css'
import '../../features/services/styles/goodsReceipts.css'

const money = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })
const dateTime = (value) => value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value)) : '-'
const states = { Draft: { label: 'Nháp', color: 'default' }, Posted: { label: 'Đã nhập kho', color: 'success' }, Cancelled: { label: 'Đã hủy', color: 'error' } }
const stateTag = (status) => <Tag color={states[status]?.color}>{states[status]?.label || status}</Tag>
const lineColumns = [
  { title: 'Sản phẩm', dataIndex: 'productName' },
  { title: 'Số lượng', dataIndex: 'quantity', align: 'right' },
  { title: 'Giá nhập / đơn vị', dataIndex: 'unitCost', align: 'right', render: (value) => money.format(value) },
  { title: 'Thành tiền', dataIndex: 'lineAmount', align: 'right', render: (value) => money.format(value) },
]

function ProductPicker(props) {
  const [search, setSearch] = useState('')
  const products = useServices({ searchTerm: search || undefined, pageIndex: 1, pageSize: 100 })
  return <Select {...props} showSearch filterOption={false} onSearch={setSearch} loading={products.isFetching}
    placeholder="Tìm sản phẩm theo dõi tồn kho" aria-label="Sản phẩm nhập kho"
    options={(products.data?.items || []).filter((product) => product.stockQuantity != null).map((product) => ({ value: product.id, label: product.name }))}
    notFoundContent={products.isError ? 'Không tải được sản phẩm. Hãy tìm lại.' : products.isFetching ? 'Đang tải…' : 'Không có sản phẩm phù hợp'} />
}

function CreateReceiptDrawer({ open, onClose, onCreated }) {
  const [form] = Form.useForm()
  const requestId = useRef(null)
  const submittedPayload = useRef(null)
  const rows = Form.useWatch('details', form) || []
  const total = rows.reduce((sum, line) => sum + (line?.quantity || 0) * (line?.unitCost || 0), 0)
  const create = useMutation({ mutationFn: goodsReceiptService.create, retry: false,
    onSuccess: (receipt) => { submittedPayload.current = null; onCreated(receipt) } })
  const uncertainResult = Boolean(create.error && !create.error.response)

  const begin = () => {
    requestId.current = crypto.randomUUID()
    submittedPayload.current = null
    create.reset()
    form.resetFields()
    form.setFieldsValue({ receivedAt: dayjs(), details: [{ quantity: 1 }] })
  }
  const save = (values) => {
    if (create.isPending) return
    // Keep the same ID and payload until an uncertain create has been resolved.
    submittedPayload.current ??= {
      id: requestId.current, receiptCode: values.receiptCode.trim(), receivedAt: values.receivedAt.toISOString(),
      supplierName: values.supplierName?.trim() || null, note: values.note?.trim() || null,
      details: values.details.map(({ productId, quantity, unitCost }) => ({ productId, quantity, unitCost })),
    }
    create.mutate(submittedPayload.current, { onError: (error) => {
      if (error.response) submittedPayload.current = null
    } })
  }

  return <Drawer title="Tạo phiếu nhập hàng" open={open} onClose={() => !create.isPending && onClose()}
    afterOpenChange={(visible) => { if (visible) begin() }} forceRender size="large"
    closable={!create.isPending} mask={{ closable: !create.isPending }}>
    <Alert type="info" showIcon message="Lưu nháp chưa tăng tồn kho. Giá nhập được lưu theo từng phiếu và không thay đổi giá bán/thuê." />
    {create.error && <Alert className="service-inline-error" type="error" showIcon message="Chưa lưu được phiếu"
      description={getServiceErrorMessage(create.error, 'Không xác định được kết quả. Bấm lưu lại để kiểm tra và nhận phiếu đã tạo.')} />}
    {uncertainResult && <Alert type="warning" showIcon message="Phiếu có thể đã được lưu. Bấm lưu lại để xác minh cùng phiếu trước khi thay đổi nội dung." />}
    <Form form={form} layout="vertical" onFinish={save} disabled={create.isPending || uncertainResult}>
      <Form.Item name="receiptCode" label="Mã phiếu nhập" rules={[{ required: true, whitespace: true, max: 50, message: 'Nhập mã phiếu, tối đa 50 ký tự.' }]}><Input maxLength={50} /></Form.Item>
      <Form.Item name="receivedAt" label="Thời điểm nhận hàng" rules={[{ required: true, message: 'Chọn thời điểm nhận hàng.' }]}><DatePicker showTime format="DD/MM/YYYY HH:mm" className="service-full-width" /></Form.Item>
      <Form.Item name="supplierName" label="Nhà cung cấp" rules={[{ max: 255 }]}><Input maxLength={255} /></Form.Item>
      <Form.List name="details" rules={[{ validator: (_, lines) => lines?.length > 0 && lines.length <= 100
        ? Promise.resolve() : Promise.reject(new Error('Phiếu cần từ 1 đến 100 dòng hàng.')) }]}>
        {(fields, { add, remove }, { errors }) => <>
          {fields.map(({ key, name, ...field }) => <div key={key} className="goods-receipt-line">
            <Form.Item {...field} name={[name, 'productId']} label="Sản phẩm" rules={[{ required: true, message: 'Chọn sản phẩm.' },
              { validator: (_, value) => !value || form.getFieldValue('details').filter((line) => line?.productId === value).length <= 1
                ? Promise.resolve() : Promise.reject(new Error('Sản phẩm đã có trong phiếu.')) }]}><ProductPicker /></Form.Item>
            <Form.Item {...field} name={[name, 'quantity']} label="Số lượng" rules={[{ required: true, type: 'integer', min: 1, max: 2147483647, message: 'Nhập số nguyên dương hợp lệ.' }]}><InputNumber min={1} max={2147483647} precision={0} className="service-full-width" /></Form.Item>
            <Form.Item {...field} name={[name, 'unitCost']} label="Giá nhập / đơn vị (VNĐ)" rules={[{ required: true, type: 'number', min: 0, max: Number.MAX_SAFE_INTEGER / 100, message: 'Nhập giá không âm trong giới hạn cho phép.' }]}><InputNumber min={0} max={Number.MAX_SAFE_INTEGER / 100} precision={2} className="service-full-width" /></Form.Item>
            <Button danger type="text" icon={<DeleteOutlined />} aria-label={`Xóa dòng ${name + 1}`} onClick={() => remove(name)} disabled={create.isPending}>Xóa</Button>
          </div>)}
          <Form.ErrorList errors={errors} />
          <Button icon={<PlusOutlined />} onClick={() => add({ quantity: 1 })} disabled={fields.length >= 100 || create.isPending}>Thêm hàng</Button>
        </>}
      </Form.List>
      <Typography.Paragraph strong>Tổng tiền nhập: {money.format(total)}</Typography.Paragraph>
      <Form.Item name="note" label="Ghi chú" rules={[{ max: 2000 }]}><Input.TextArea rows={3} maxLength={2000} /></Form.Item>
      <Button type="primary" htmlType="submit" loading={create.isPending} disabled={create.isPending}>Lưu phiếu nháp</Button>
    </Form>
  </Drawer>
}

export default function GoodsReceiptsPage() {
  const queryClient = useQueryClient()
  const [searchParams, setSearchParams] = useSearchParams()
  const selectedId = searchParams.get('receiptId')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState()
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [createOpen, setCreateOpen] = useState(false)
  const list = useQuery({ queryKey: ['goods-receipts', { search, status, pageIndex, pageSize }],
    queryFn: () => goodsReceiptService.getAll({ searchTerm: search || undefined, status, pageIndex, pageSize }) })
  const detail = useQuery({ queryKey: ['goods-receipt', selectedId], queryFn: () => goodsReceiptService.getById(selectedId), enabled: Boolean(selectedId) })
  const refresh = async () => {
    await Promise.all(['goods-receipts', 'goods-receipt', 'services', 'inventory-history'].map((key) => queryClient.invalidateQueries({ queryKey: [key] })))
  }
  const action = useMutation({ mutationFn: ({ id, operation }) => goodsReceiptService[operation](id), retry: false,
    onSuccess: async (receipt) => { queryClient.setQueryData(['goods-receipt', receipt.id], receipt); await refresh(); message.success(receipt.status === 'Posted' ? 'Đã nhập kho.' : 'Đã hủy phiếu nháp.') },
    onError: async () => { await refresh() } })
  const receipt = detail.data
  const columns = [
    { title: 'Mã phiếu', dataIndex: 'receiptCode', render: (code, row) => <Button type="link" onClick={() => { action.reset(); setSearchParams({ receiptId: row.id }) }}>{code}</Button> },
    { title: 'Nhận hàng', dataIndex: 'receivedAt', render: dateTime },
    { title: 'Nhà cung cấp', dataIndex: 'supplierName', render: (value) => value || '-' },
    { title: 'Trạng thái', dataIndex: 'status', render: stateTag },
    { title: 'Tổng tiền nhập', dataIndex: 'totalAmount', align: 'right', render: (value) => money.format(value) },
  ]

  return <div className="service-list-page">
    <ServiceListHeader title="Phiếu nhập hàng" action={<Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>Tạo phiếu nhập</Button>} />
    <Space wrap className="goods-receipt-filters">
      <Input.Search allowClear placeholder="Tìm mã phiếu / nhà cung cấp" aria-label="Tìm phiếu nhập" onSearch={(value) => { setSearch(value); setPageIndex(1) }} />
      <Select allowClear placeholder="Tất cả trạng thái" aria-label="Trạng thái phiếu nhập" value={status} onChange={(value) => { setStatus(value); setPageIndex(1) }} options={Object.entries(states).map(([value, item]) => ({ value, label: item.label }))} />
      <Button icon={<ReloadOutlined />} onClick={refresh} loading={list.isFetching}>Làm mới</Button>
    </Space>
    <section className="service-list-table">
      {list.isError ? <Empty description="Không tải được phiếu nhập."><Button onClick={list.refetch}>Thử lại</Button></Empty>
        : <Table rowKey="id" loading={list.isLoading} columns={columns} dataSource={list.data?.items || []} scroll={{ x: 750 }}
          pagination={{ current: pageIndex, pageSize, total: list.data?.totalCount || 0, showSizeChanger: true, onChange: (page, size) => { setPageIndex(page); setPageSize(size) } }} />}
    </section>
    <CreateReceiptDrawer open={createOpen} onClose={() => setCreateOpen(false)} onCreated={async (created) => {
      setCreateOpen(false); setSearchParams({ receiptId: created.id }); await refresh(); message.success('Đã lưu phiếu nháp. Kiểm tra và xác nhận nhập kho.')
    }} />
    <Drawer title={`Phiếu nhập ${receipt?.receiptCode || ''}`} open={Boolean(selectedId)} onClose={() => { if (!action.isPending) { action.reset(); setSearchParams({}) } }} size="large"
      closable={!action.isPending} mask={{ closable: !action.isPending }}>
      {detail.isLoading && <Spin />}
      {detail.isError && <Alert type="error" message="Không tải được phiếu nhập" action={<Button onClick={detail.refetch}>Thử lại</Button>} />}
      {action.error && <Alert className="service-inline-error" type="error" showIcon message="Không thể xử lý phiếu"
        description={getServiceErrorMessage(action.error, 'Không xác định được kết quả. Kiểm tra trạng thái sau khi làm mới.')} />}
      {receipt && <>
        <Descriptions column={1} bordered items={[
          { key: 'status', label: 'Trạng thái', children: stateTag(receipt.status) },
          { key: 'supplier', label: 'Nhà cung cấp', children: receipt.supplierName || '-' },
          { key: 'received', label: 'Nhận hàng', children: dateTime(receipt.receivedAt) },
          { key: 'created', label: 'Tạo phiếu', children: dateTime(receipt.createdAt) },
          { key: 'updated', label: receipt.status === 'Posted' ? 'Xác nhận nhập kho' : 'Cập nhật', children: dateTime(receipt.updatedAt) },
          { key: 'note', label: 'Ghi chú', children: receipt.note || '-' },
        ]} />
        <Table className="goods-receipt-details" rowKey="id" columns={lineColumns} dataSource={receipt.details} pagination={false} scroll={{ x: 600 }} />
        <Typography.Paragraph strong>Tổng tiền nhập: {money.format(receipt.totalAmount)}</Typography.Paragraph>
        {receipt.status === 'Draft' ? <Space wrap>
          <Popconfirm title="Xác nhận nhập kho?" description="Tồn kho sẽ tăng. Số lượng và giá nhập của phiếu sẽ được giữ cố định." okText="Nhập kho" cancelText="Đóng"
            onConfirm={() => action.mutateAsync({ id: receipt.id, operation: 'post' }).catch(() => {})}>
            <Button type="primary" loading={action.isPending} disabled={detail.isFetching}>Xác nhận nhập kho</Button>
          </Popconfirm>
          <Popconfirm title="Hủy phiếu nháp?" okText="Hủy phiếu" cancelText="Giữ lại" onConfirm={() => action.mutateAsync({ id: receipt.id, operation: 'cancel' }).catch(() => {})}>
            <Button danger disabled={action.isPending || detail.isFetching}>Hủy phiếu nháp</Button>
          </Popconfirm>
        </Space> : <Alert type="info" message={receipt.status === 'Posted' ? 'Phiếu đã nhập kho được giữ nguyên để đối chiếu lịch sử giá nhập.' : 'Phiếu đã hủy không làm thay đổi tồn kho.'} />}
      </>}
    </Drawer>
  </div>
}

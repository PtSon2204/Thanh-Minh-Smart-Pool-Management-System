import { Alert, Button, Drawer, Form, Input, InputNumber, Pagination, Select, Typography } from 'antd'
import { useEffect, useRef, useState } from 'react'
import { getRentalErrorMessage } from '../hooks/useRentalMutations'
import { useServices } from '../../services/hooks/useServices'
import '../styles/rentalCheckout.css'

const PRODUCT_PAGE_SIZE = 20

export default function RentalCheckoutDrawer({ open, mutation, onClose, onSuccess }) {
  const [form] = Form.useForm()
  const submitting = useRef(false)
  const [searchTerm, setSearchTerm] = useState('')
  const [pageIndex, setPageIndex] = useState(1)
  const [selectedProduct, setSelectedProduct] = useState(null)
  const productsQuery = useServices({ type: 'Rental', isActive: true, searchTerm: searchTerm || undefined, pageIndex, pageSize: PRODUCT_PAGE_SIZE })
  const { refetch: refetchProducts } = productsQuery
  const quantity = Form.useWatch('quantity', form) || 0
  const trackedStock = selectedProduct?.stockQuantity

  useEffect(() => {
    if (open) refetchProducts()
  }, [open, refetchProducts])

  const options = (productsQuery.data?.items || []).map((product) => {
    const tracked = Number.isInteger(product.stockQuantity)
    const available = tracked && product.stockQuantity > 0
    return { value: product.id, label: product.name, stockLabel: tracked ? `Tồn: ${product.stockQuantity}` : 'Không theo dõi', disabled: !available }
  })

  const handleSearch = (value) => {
    setSearchTerm(value)
    setPageIndex(1)
  }

  const handleProductChange = (productId) => {
    setSelectedProduct((productsQuery.data?.items || []).find((product) => product.id === productId) || null)
    form.setFieldValue('quantity', 1)
  }

  const handleFinish = (values) => {
    if (submitting.current || mutation.isPending) return
    submitting.current = true
    mutation.reset()
    mutation.mutate({ productId: values.productId, quantity: values.quantity, customerName: values.customerName?.trim() || null, customerPhone: values.customerPhone?.trim() || null, depositPerUnit: values.depositPerUnit || 0 }, { onSuccess: () => { form.resetFields(); onSuccess() }, onSettled: () => { submitting.current = false } })
  }

  const close = () => {
    if (!mutation.isPending) {
      mutation.reset()
      form.resetFields()
      onClose()
    }
  }

  const popupRender = (originNode) => <div>{originNode}<div className="rental-product-pagination" onMouseDown={(event) => event.stopPropagation()}><span>Trang {pageIndex}/{Math.max(1, productsQuery.data?.totalPages || 1)}</span><Pagination simple size="small" current={pageIndex} pageSize={PRODUCT_PAGE_SIZE} total={productsQuery.data?.totalCount || 0} disabled={productsQuery.isLoading} onChange={setPageIndex} /></div></div>
  const optionRender = (option) => <div className="rental-product-option"><span title={option.label}>{option.label}</span><small>{option.data.stockLabel}</small></div>

  return <Drawer rootClassName="rental-checkout-drawer" title="Tạo đơn thuê" open={open} onClose={close} closable={!mutation.isPending} mask={{ closable: !mutation.isPending }} size="large">
    <Typography.Paragraph>Chọn sản phẩm và số lượng cần cho thuê. Hệ thống sẽ trừ tồn kho sau khi tạo đơn.</Typography.Paragraph>
    <Form form={form} layout="vertical" preserve={false} onFinish={handleFinish} initialValues={{ quantity: 1, depositPerUnit: 0 }}>
      {mutation.error && <Alert className="service-inline-error" type="error" showIcon message="Không thể tạo đơn thuê" description={getRentalErrorMessage(mutation.error, 'Máy chủ không thể tạo đơn thuê.')} />}
      <Form.Item label="Sản phẩm cho thuê" name="productId" rules={[{ required: true, message: 'Chọn sản phẩm cho thuê.' }]}><Select showSearch filterOption={false} onSearch={handleSearch} onChange={handleProductChange} optionRender={optionRender} popupRender={popupRender} placeholder="Tìm và chọn sản phẩm" options={options} loading={productsQuery.isLoading} disabled={mutation.isPending} notFoundContent={productsQuery.isError ? 'Không thể tải sản phẩm.' : 'Không có sản phẩm đủ tồn kho.'} /></Form.Item>
      <div className="rental-stock-preview" aria-label="Xem trước tồn kho">
        <div><span>Tồn có thể cho thuê</span><strong>{Number.isInteger(trackedStock) ? trackedStock : 'Chọn sản phẩm có theo dõi tồn kho'}</strong></div>
        <div><span>Tồn sau khi tạo đơn</span><strong>{Number.isInteger(trackedStock) ? Math.max(0, trackedStock - quantity) : '-'}</strong></div>
      </div>
      <Form.Item label="Số lượng" name="quantity" rules={[{ required: true, message: 'Nhập số lượng.' }, { validator: (_, value) => Number.isInteger(value) && value > 0 && Number.isInteger(trackedStock) && value <= trackedStock ? Promise.resolve() : Promise.reject(new Error('Số lượng phải là số nguyên và không vượt tồn kho.')) }]}><InputNumber min={1} precision={0} className="service-full-width" disabled={mutation.isPending || !Number.isInteger(trackedStock)} /></Form.Item>
      <Form.Item label="Tiền cọc mỗi đơn vị (VNĐ)" name="depositPerUnit" rules={[{ type: 'number', min: 0, message: 'Tiền cọc không được âm.' }]}><InputNumber min={0} precision={0} className="service-full-width" disabled={mutation.isPending} /></Form.Item>
      <Form.Item label="Tên khách hàng" name="customerName" rules={[{ max: 255, message: 'Tên không vượt quá 255 ký tự.' }]}><Input disabled={mutation.isPending} /></Form.Item>
      <Form.Item label="Số điện thoại" name="customerPhone" rules={[{ max: 20, message: 'Số điện thoại không vượt quá 20 ký tự.' }]}><Input disabled={mutation.isPending} /></Form.Item>
      <Button className="admin-action-submit" type="primary" htmlType="submit" loading={mutation.isPending} disabled={mutation.isPending || productsQuery.isLoading || !Number.isInteger(trackedStock)}>Tạo đơn thuê</Button>
    </Form>
  </Drawer>
}

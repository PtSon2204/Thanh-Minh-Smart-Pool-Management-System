import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Alert, Button, Checkbox, Drawer, Form, Input, InputNumber, Pagination, Select, Typography } from 'antd'
import { useEffect, useMemo, useRef, useState } from 'react'
import { getRentalErrorMessage, useRentalQuote } from '../hooks/useRentalMutations'
import { createRentalRequestId, isRentalRequestOutcomeUncertain } from '../utils/rentalRequests'
import { useServices } from '../../services/hooks/useServices'
import '../styles/rentalCheckout.css'

const PRODUCT_PAGE_SIZE = 20
const DEPOSIT_PER_UNIT = 20000

function itemsSignature(items) {
  return JSON.stringify((items || []).map(({ productId, quantity }) => ({ productId, quantity })))
}

function freezeCheckoutPayload(payload) {
  return Object.freeze({ ...payload, items: Object.freeze(payload.items.map((item) => Object.freeze({ ...item }))) })
}

export default function RentalCheckoutDrawer({ open, mutation, onClose, onSuccess }) {
  const [form] = Form.useForm()
  const submitting = useRef(false)
  const quoteVersion = useRef(0)
  const [searchTerm, setSearchTerm] = useState('')
  const [pageIndex, setPageIndex] = useState(1)
  const [quote, setQuote] = useState(null)
  const [quoteSignature, setQuoteSignature] = useState(null)
  const [frozenCheckout, setFrozenCheckout] = useState(null)
  const quoteMutation = useRentalQuote()
  const resetQuoteMutation = quoteMutation.reset
  const formItems = Form.useWatch('items', form) || []
  const productsQuery = useServices({ isActive: true, searchTerm: searchTerm || undefined, pageIndex, pageSize: PRODUCT_PAGE_SIZE })
  const { refetch: refetchProducts } = productsQuery
  const productById = useMemo(() => new Map((productsQuery.data?.items || []).map((product) => [product.id, product])), [productsQuery.data?.items])
  const currentSignature = itemsSignature(formItems)
  const quoteIsCurrent = Boolean(quote && quoteSignature === currentSignature)
  const isLocked = Boolean(frozenCheckout)
  const isPending = mutation.isPending || quoteMutation.isPending

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

  const clearQuote = () => {
    if (isLocked) return
    resetQuoteMutation()
    mutation.reset()
    quoteVersion.current += 1
    setQuote(null)
    setQuoteSignature(null)
    form.setFieldValue('collectionConfirmed', false)
  }

  const requestQuote = async () => {
    if (isLocked || quoteMutation.isPending) return
    form.setFieldValue('collectionConfirmed', false)
    try {
      const values = await form.validateFields(['items'])
      const items = values.items.map(({ productId, quantity }) => ({ productId, quantity }))
      quoteVersion.current += 1
      const requestVersion = quoteVersion.current
      resetQuoteMutation()
      quoteMutation.mutate({ items }, {
        onSuccess: (result) => {
          if (quoteVersion.current !== requestVersion) return
          setQuote(result)
          setQuoteSignature(itemsSignature(items))
        },
      })
    } catch {
      return null
    }
  }

  const submitCheckout = async () => {
    if (submitting.current || mutation.isPending) return
    submitting.current = true
    let payload = frozenCheckout

    if (!payload) {
      try {
        const values = await form.validateFields()
        if (!quoteIsCurrent) { submitting.current = false; return }
        const quotedPrices = new Map(quote.items.map((item) => [item.productId, item.unitPrice]))
        payload = freezeCheckoutPayload({
          requestId: createRentalRequestId(),
          customerName: values.customerName.trim(),
          customerPhone: values.customerPhone.trim(),
          collectionConfirmed: true,
          items: values.items.map(({ productId, quantity }) => ({ productId, quantity, expectedUnitPrice: quotedPrices.get(productId) })),
        })
        setFrozenCheckout(payload)
      } catch {
        submitting.current = false
        return
      }
    }

    mutation.reset()
    mutation.mutate(payload, {
      onSuccess: () => {
        setFrozenCheckout(null)
        setQuote(null)
        setQuoteSignature(null)
        form.resetFields()
        onSuccess()
      },
      onError: (error) => {
        if (!isRentalRequestOutcomeUncertain(error)) {
          quoteVersion.current += 1
          setFrozenCheckout(null)
          setQuote(null)
          setQuoteSignature(null)
        }
      },
      onSettled: () => { submitting.current = false },
    })
  }

  const close = () => {
    if (!isLocked && !mutation.isPending) onClose()
  }

  const popupRender = (originNode) => <div>{originNode}<div className="rental-product-pagination" onMouseDown={(event) => event.stopPropagation()}><span>Trang {pageIndex}/{Math.max(1, productsQuery.data?.totalPages || 1)}</span><Pagination simple size="small" current={pageIndex} pageSize={PRODUCT_PAGE_SIZE} total={productsQuery.data?.totalCount || 0} disabled={productsQuery.isLoading || isLocked} onChange={setPageIndex} /></div></div>
  const optionRender = (option) => <div className="rental-product-option"><span title={option.label}>{option.label}</span><small>{option.data.stockLabel}</small></div>

  return <Drawer rootClassName="rental-checkout-drawer" title="Tạo đơn thuê" open={open} onClose={close} closable={!mutation.isPending && !isLocked} mask={{ closable: !mutation.isPending && !isLocked }} size="large">
    <Typography.Paragraph>Nhập các nhóm sản phẩm, nhận báo giá từ máy chủ, rồi xác nhận đã thu tiền trước khi xuất đồ.</Typography.Paragraph>
    <Form form={form} layout="vertical" preserve={false} initialValues={{ items: [{ quantity: 1 }], collectionConfirmed: false }} onFinish={submitCheckout} disabled={isLocked} onValuesChange={(changed) => { if (Object.keys(changed).some((key) => key !== 'collectionConfirmed')) clearQuote() }}>
      {mutation.error && <Alert className="service-inline-error" type="error" showIcon message="Không thể xác nhận xuất đồ" description={isLocked ? 'Kết quả thu tiền chưa xác định. Hãy thử lại đúng yêu cầu đã được giữ nguyên.' : getRentalErrorMessage(mutation.error, 'Máy chủ không thể xác nhận đơn thuê.')} />}
      {quoteMutation.error && <Alert className="service-inline-error" type="error" showIcon message="Không thể tạo báo giá" description={getRentalErrorMessage(quoteMutation.error, 'Máy chủ không thể tạo báo giá.')} />}
      {isLocked && <Alert className="service-inline-error" type="warning" showIcon message="Yêu cầu thu tiền đã được giữ nguyên" description="Bạn chỉ có thể thử lại cùng yêu cầu này. Không tạo yêu cầu mới khi kết quả trước đó chưa xác định." />}
      <Form.List name="items" rules={[{ validator: (_, value) => {
        const productIds = (value || []).map((item) => item?.productId).filter(Boolean)
        return productIds.length === new Set(productIds).size ? Promise.resolve() : Promise.reject(new Error('Mỗi sản phẩm chỉ được chọn một lần.'))
      } }]}>
        {(fields, { add, remove }, { errors }) => <>
          {fields.map((field, index) => {
            const selectedProduct = productById.get(formItems[index]?.productId)
            return <section className="rental-checkout-line" key={field.key} aria-label={`Sản phẩm thuê ${index + 1}`}>
              <div className="rental-checkout-line-heading"><strong>Sản phẩm {index + 1}</strong>{fields.length > 1 && <Button type="text" danger icon={<DeleteOutlined />} onClick={() => remove(field.name)} disabled={isLocked || isPending}>Xóa</Button>}</div>
              <Form.Item label="Sản phẩm cho thuê" name={[field.name, 'productId']} rules={[{ required: true, message: 'Chọn sản phẩm cho thuê.' }]}>
                <Select showSearch filterOption={false} onSearch={handleSearch} optionRender={optionRender} popupRender={popupRender} placeholder="Tìm và chọn sản phẩm" options={options} loading={productsQuery.isLoading} disabled={isPending || isLocked} notFoundContent={productsQuery.isError ? 'Không thể tải sản phẩm.' : 'Không có sản phẩm đủ tồn kho.'} />
              </Form.Item>
              <Form.Item label="Số lượng" name={[field.name, 'quantity']} rules={[{ required: true, message: 'Nhập số lượng.' }, { type: 'integer', min: 1, message: 'Số lượng phải là số nguyên dương.' }, { validator: (_, value) => !selectedProduct || value <= selectedProduct.stockQuantity ? Promise.resolve() : Promise.reject(new Error('Số lượng không được vượt tồn kho hiện có.')) }]}>
                <InputNumber min={1} precision={0} className="service-full-width" disabled={isPending || isLocked} />
              </Form.Item>
              <span className="rental-line-stock">{selectedProduct ? `Tồn hiện có: ${selectedProduct.stockQuantity}` : 'Chọn sản phẩm để xem tồn kho.'}</span>
            </section>
          })}
          <Form.ErrorList errors={errors} />
          <Button className="rental-add-line" icon={<PlusOutlined />} onClick={() => add({ quantity: 1 })} disabled={isPending || isLocked}>Thêm sản phẩm</Button>
        </>}
      </Form.List>
      <Form.Item label="Tên khách hàng" name="customerName" rules={[{ required: true, whitespace: true, message: 'Nhập tên khách hàng.' }, { max: 255, message: 'Tên không vượt quá 255 ký tự.' }]}><Input disabled={isPending || isLocked} /></Form.Item>
      <Form.Item label="Số điện thoại" name="customerPhone" rules={[{ required: true, whitespace: true, message: 'Nhập số điện thoại.' }, { max: 20, message: 'Số điện thoại không vượt quá 20 ký tự.' }]}><Input disabled={isPending || isLocked} /></Form.Item>
      <Form.Item label="Cọc cố định mỗi đơn vị"><Input readOnly value={currencyFormatter.format(DEPOSIT_PER_UNIT)} /></Form.Item>
      <Button className="admin-action-submit" onClick={requestQuote} loading={quoteMutation.isPending} disabled={isPending || isLocked || productsQuery.isLoading}>Nhận báo giá</Button>
      {quoteIsCurrent && <section className="rental-quote-preview" aria-label="Báo giá thuê">
        <Typography.Title level={5}>Báo giá từ máy chủ</Typography.Title>
        <div className="rental-quote-lines">{quote.items.map((item) => <div key={item.productId}><strong>{item.productName}</strong><span>{item.quantity} x {currencyFormatter.format(item.unitPrice)}</span><span>Phí thuê: {currencyFormatter.format(item.rentalFee)}</span><span>Cọc: {currencyFormatter.format(item.depositAmount)}</span></div>)}</div>
        <div className="rental-quote-totals"><span>Phí thuê <strong>{currencyFormatter.format(quote.rentalFeeTotal)}</strong></span><span>Cọc hoàn lại ({currencyFormatter.format(quote.depositPerUnit || DEPOSIT_PER_UNIT)}/đơn vị) <strong>{currencyFormatter.format(quote.depositTotal)}</strong></span><span>Tổng cần thu <strong>{currencyFormatter.format(quote.totalToCollect)}</strong></span></div>
      </section>}
      <Form.Item name="collectionConfirmed" valuePropName="checked" rules={[{ validator: (_, value) => value ? Promise.resolve() : Promise.reject(new Error('Xác nhận đã thu đủ tiền trước khi xuất đồ.')) }]}>
        <Checkbox disabled={isPending || isLocked}>Tôi xác nhận đã thu đủ tổng tiền theo báo giá và được phép xuất đồ.</Checkbox>
      </Form.Item>
      <Button className="admin-action-submit" type="primary" htmlType="submit" loading={mutation.isPending} disabled={isPending || (!quoteIsCurrent && !isLocked)}>{isLocked ? 'Thử lại yêu cầu đã giữ' : 'Xác nhận thu tiền và xuất đồ'}</Button>
    </Form>
  </Drawer>
}

const currencyFormatter = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })

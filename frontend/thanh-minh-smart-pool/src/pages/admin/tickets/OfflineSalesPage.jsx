import { useState, useEffect } from 'react'
import { createPortal } from 'react-dom'
import { Button, Row, Col, Typography, Card, InputNumber, Modal, Spin, Result, Divider, List, Input, Form, Space, message, Tag, Select, Pagination, Segmented, Empty } from 'antd'
import { ShoppingCartOutlined, PrinterOutlined, CheckCircleFilled, DeleteOutlined, PhoneOutlined, BankOutlined, DollarOutlined, LoadingOutlined, CloseCircleOutlined, CheckCircleOutlined, SearchOutlined } from '@ant-design/icons'
import { QRCodeSVG } from 'qrcode.react'
import { useTicketTypes } from "../../../features/tickets/hooks/useTicketTypes"
import { useSellOfflineTicket, useCreatePendingOrder, useOrderStatus } from "../../../features/tickets/hooks/useSellOfflineTicket"
import { useVouchers } from "../../../features/vouchers/hooks/useVouchers"
import { useVoucherClock } from "../../../features/vouchers/hooks/useVoucherClock"
import { getVoucherDiscount, getVoucherError, VoucherStatus } from "../../../features/vouchers/utils/voucherValidity"


const { Title } = Typography

export default function OfflineSalesPage() {
  const [cart, setCart] = useState([])
  const [customerPhone, setCustomerPhone] = useState('')

  // Voucher state
  const [voucherCode, setVoucherCode] = useState('')
  const [appliedVoucher, setAppliedVoucher] = useState(null) // { id, code, discountType, discountValue, minOrderValue }
  const [voucherError, setVoucherError] = useState('')

  // Modals
  const [paymentMethodVisible, setPaymentMethodVisible] = useState(false)
  const [qrModalVisible, setQrModalVisible] = useState(false)
  const [receiptVisible, setReceiptVisible] = useState(false)

  // Order state
  const [pendingOrderId, setPendingOrderId] = useState(null)
  const [transactionRef, setTransactionRef] = useState(null)
  
  const [soldTickets, setSoldTickets] = useState([])
  const [soldOrder, setSoldOrder] = useState(null)
  const [generatedAccount, setGeneratedAccount] = useState(null)

  // Filters & Pagination state cho catalog POS
  const [searchTerm, setSearchTerm] = useState('')
  const [categoryFilter, setCategoryFilter] = useState('ALL')
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(6)

  const { data: pagedData, isLoading } = useTicketTypes({
    isActive: true,
    searchTerm: searchTerm.trim() || undefined,
    category: categoryFilter !== 'ALL' ? categoryFilter : undefined,
    pageIndex,
    pageSize,
  })

  const { data: voucherData, isLoading: vouchersLoading } = useVouchers({
    status: VoucherStatus.ACTIVE,
    pageIndex: 1,
    pageSize: 100,
  })
  const availableVouchers = voucherData?.items || []
  const now = useVoucherClock()
  
  const ticketTypes = pagedData?.items || []
  const totalCount = pagedData?.totalCount || 0

  const { mutate: sellTicket, isPending: isSellingCash } = useSellOfflineTicket()
  const { mutate: createPendingOrder, isPending: isCreatingOrder } = useCreatePendingOrder()

  // Polling hook
  const { data: orderStatusData } = useOrderStatus(pendingOrderId, {
    refetchInterval: (query) => {
      // Stop polling if completed or error
      if (query?.state?.data?.status === 'COMPLETED') return false;
      return qrModalVisible ? 3000 : false;
    }
  })

  // Watch for order completion or cancellation
  useEffect(() => {
    if (orderStatusData && orderStatusData.status === 'COMPLETED') {
      message.success('Đã nhận được tiền thanh toán!');
      try { sessionStorage.removeItem('pos_pending_order'); } catch { /* ignore */ }
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setSoldTickets(orderStatusData.tickets || []);
      setQrModalVisible(false);
      setPendingOrderId(null);
      setReceiptVisible(true);
      setCart([]);
      setCustomerPhone('');
    } else if (orderStatusData && (orderStatusData.status === 'CANCELLED' || orderStatusData.status === 'EXPIRED')) {
      message.error('Giao dịch đã hết hạn hoặc bị hủy.');
      try { sessionStorage.removeItem('pos_pending_order'); } catch { /* ignore */ }
      setQrModalVisible(false);
      setPendingOrderId(null);
    }
  }, [orderStatusData])

  // Khôi phục đơn hàng QR nếu nhân viên reload trang
  useEffect(() => {
    try {
      const savedPending = sessionStorage.getItem('pos_pending_order');
      if (savedPending) {
        const parsed = JSON.parse(savedPending);
        const elapsedSeconds = (Date.now() - (parsed.createdAt || 0)) / 1000;
        if (elapsedSeconds < 300) {
          // eslint-disable-next-line react-hooks/set-state-in-effect
          setPendingOrderId(parsed.orderId);
          setTransactionRef(parsed.transactionRef);
          setSoldOrder(parsed.soldOrder);
          setGeneratedAccount(parsed.accountInfo || null);
          setQrModalVisible(true);
        } else {
          sessionStorage.removeItem('pos_pending_order');
        }
      }
    } catch {
      // ignore
    }
  }, [])

  const handleSelectTicket = (tt) => {
    setCart((prev) => {
      const existing = prev.find(item => item.ticketType.id === tt.id)
      if (existing) {
        return prev.map(item => item.ticketType.id === tt.id ? { ...item, quantity: item.quantity + 1 } : item)
      }
      return [...prev, { ticketType: tt, quantity: 1, requestedEntries: 1 }]
    })
  }

  const updateQuantity = (id, q) => {
    if (q < 1) return
    setCart(prev => prev.map(item => item.ticketType.id === id ? { ...item, quantity: q } : item))
  }

  const updateRequestedEntries = (id, entries) => {
    const valid = entries === '' || entries === null || entries === undefined
      ? 1
      : Math.max(1, parseInt(entries, 10) || 1)
    setCart(prev => prev.map(item => item.ticketType.id === id ? { ...item, requestedEntries: valid } : item))
  }

  const removeItem = (id) => {
    setCart(prev => prev.filter(item => item.ticketType.id !== id))
  }

  const totalAmount = cart.reduce((sum, item) => {
    const entriesMultiplier = item.ticketType.ticketCategory === 'VE_LUOT' ? (item.requestedEntries || 1) : 1
    return sum + (item.ticketType.price * entriesMultiplier * item.quantity)
  }, 0)

  const currentVoucher = availableVouchers.find(v => v.id === appliedVoucher?.id)
  const appliedVoucherError = appliedVoucher ? getVoucherError(currentVoucher, totalAmount, now) : null
  const discountAmount = getVoucherDiscount(currentVoucher, totalAmount, now, true)
  const validVouchers = availableVouchers.filter(v => !getVoucherError(v, totalAmount, now))

  const finalAmount = totalAmount - discountAmount
  const formattedTotal = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(finalAmount)

  const handleApplyVoucher = async () => {
    const code = voucherCode.trim().toUpperCase()
    if (!code) return
    setVoucherError('')
    try {
      const matched = availableVouchers.find(v => v.code === code)
      const error = getVoucherError(matched, totalAmount)
      if (error) {
        setVoucherError(error)
        setAppliedVoucher(null)
        return
      }
      setAppliedVoucher(matched)
      message.success(`Áp mã "${code}" thành công!`)
    } catch {
      setVoucherError('Không thể kiểm tra mã. Vui lòng thử lại.')
    }
  }

  const handleRemoveVoucher = () => {
    setAppliedVoucher(null)
    setVoucherCode('')
    setVoucherError('')
  }


  // VE_THANG hoặc VE_LUOT > 1 lượt: cần đăng ký TK qua SĐT để gắn vé và lưu số lượt
  const requiresRegistration = cart.some(item => 
    item.ticketType.ticketCategory === 'VE_THANG' ||
    (item.ticketType.ticketCategory === 'VE_LUOT' && (item.requestedEntries || 1) > 1)
  )

  const isValidPhone = customerPhone ? /^0\d{9}$/.test(customerPhone.trim()) : false;
  const canCheckout = cart.length > 0 && (!requiresRegistration || isValidPhone)

  const validateAppliedVoucher = () => {
    if (!appliedVoucher) return true
    const error = getVoucherError(currentVoucher, totalAmount)
    if (!error) return true
    setVoucherError(error)
    setAppliedVoucher(null)
    setPaymentMethodVisible(false)
    message.error(error)
    return false
  }

  const handleOpenPaymentMethod = () => {
    if (!canCheckout || !validateAppliedVoucher()) return
    setPaymentMethodVisible(true)
  }

  const getItemsPayload = () => cart.map(item => ({
    ticketTypeId: item.ticketType.id,
    quantity: item.quantity,
    requestedEntries: item.ticketType.ticketCategory === 'VE_LUOT' ? (item.requestedEntries || 1) : null
  }))

  const getOrderSummary = () => cart.map(c => {
    const entriesSuffix = c.ticketType.ticketCategory === 'VE_LUOT' && (c.requestedEntries || 1) > 1 
      ? ` (${c.requestedEntries} lượt)` 
      : ''
    return `${c.ticketType.name}${entriesSuffix} x${c.quantity}`
  }).join(', ')

  const handlePayCash = () => {
    if (isSellingCash || !validateAppliedVoucher()) return;
    const payload = { 
      items: getItemsPayload(),
      customerPhone: requiresRegistration ? customerPhone.trim() : null,
      voucherCode: appliedVoucher ? appliedVoucher.code : null
    }

    sellTicket(
      payload,
      {
        onSuccess: (data) => {
          setSoldOrder({
            id: data.orderId,
            totalAmount: data.totalAmount,
            discountAmount: data.discountAmount || 0,
            finalAmount: data.finalAmount !== undefined ? data.finalAmount : data.totalAmount,
            voucherCode: data.voucherCode,
            cartSummary: getOrderSummary(),
            customerPhone: requiresRegistration ? customerPhone.trim() : ''
          })
          setSoldTickets(data.tickets)
          setGeneratedAccount(data.accountInfo || null)
          setPaymentMethodVisible(false)
          setReceiptVisible(true)
          setCart([])
          setCustomerPhone('')
          setAppliedVoucher(null)
          setVoucherCode('')
        }
      }
    )
  }

  const handlePayTransfer = () => {
    if (isCreatingOrder || !validateAppliedVoucher()) return;
    const payload = { 
      items: getItemsPayload(),
      customerPhone: requiresRegistration ? customerPhone.trim() : null,
      voucherCode: appliedVoucher ? appliedVoucher.code : null
    }

    createPendingOrder(
      payload,
      {
        onSuccess: (data) => {
          const orderData = {
            id: data.orderId,
            totalAmount: data.totalAmount,
            discountAmount: data.discountAmount || 0,
            finalAmount: data.finalAmount !== undefined ? data.finalAmount : data.totalAmount,
            voucherCode: data.voucherCode,
            cartSummary: getOrderSummary(),
            customerPhone: requiresRegistration ? customerPhone.trim() : ''
          }
          setSoldOrder(orderData)
          setGeneratedAccount(data.accountInfo || null)
          setTransactionRef(data.transactionRef)
          setPendingOrderId(data.orderId)
          
          try {
            sessionStorage.setItem('pos_pending_order', JSON.stringify({
              orderId: data.orderId,
              transactionRef: data.transactionRef,
              soldOrder: orderData,
              accountInfo: data.accountInfo || null,
              createdAt: Date.now()
            }))
          } catch {
            // ignore
          }

          setPaymentMethodVisible(false)
          setQrModalVisible(true)
          setAppliedVoucher(null)
          setVoucherCode('')
        }
      }
    )
  }

  const handlePrint = () => {
    window.print()
  }



  const bankId = import.meta.env.VITE_VIETQR_BANK_ID || '970422';
  const accNo = import.meta.env.VITE_VIETQR_ACCOUNT_NO || '';
  const accName = import.meta.env.VITE_VIETQR_ACCOUNT_NAME || 'PHAM THE SON';
  
  // Nếu khách chuyển thiếu (PARTIAL), mã QR tự cập nhật theo số tiền còn thiếu (remainingAmount)
  const currentQrAmount = (orderStatusData?.status === 'PARTIAL' && orderStatusData?.remainingAmount !== undefined)
    ? orderStatusData.remainingAmount
    : finalAmount;

  const vietQRUrl = transactionRef 
    ? `https://img.vietqr.io/image/${bankId}-${accNo}-compact2.png?amount=${currentQrAmount}&addInfo=${transactionRef}&accountName=${encodeURIComponent(accName)}` 
    : '';


  return (
    <>
      <style>
        {`
          .print-section {
            display: none;
          }
          @media print {
            body > * { display: none !important; }
            body > .print-section { 
              display: block !important; 
              width: 100% !important;
              background: white !important;
              padding: 0 !important;
              margin: 0 !important;
            }
          }
        `}
      </style>

      <div className="pos-container">
        
        {/* Left Column: Ticket Types Menu */}
        <div className="pos-catalog">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
            <Title level={4} style={{ margin: 0 }}>🎯 Màn hình bán vé (POS)</Title>
            <div style={{ color: '#64748b', fontSize: 13, fontWeight: 500 }}>
              {totalCount > 0 ? `${totalCount} loại vé` : ''}
            </div>
          </div>

          {/* Search & Filter Toolbar */}
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 16, background: '#ffffff', padding: '12px 14px', borderRadius: 12, border: '1px solid #e2e8f0', boxShadow: '0 1px 3px rgba(0,0,0,0.02)' }}>
            <Input
              prefix={<SearchOutlined style={{ color: '#0068ff' }} />}
              placeholder="Tìm kiếm theo tên vé..."
              value={searchTerm}
              onChange={e => {
                setSearchTerm(e.target.value)
                setPageIndex(1)
              }}
              allowClear
              style={{ flex: 1, minWidth: 180, borderRadius: 8, height: 38 }}
            />
            <Segmented
              options={[
                { label: 'Tất cả', value: 'ALL' },
                { label: 'Vé lượt', value: 'VE_LUOT' },
                { label: 'Vé tháng', value: 'VE_THANG' }
              ]}
              value={categoryFilter}
              onChange={val => {
                setCategoryFilter(val)
                setPageIndex(1)
              }}
              style={{ borderRadius: 8, padding: 3, fontWeight: 600 }}
            />
          </div>

          {/* Ticket Cards Grid with Scroll */}
          <div style={{ flex: 1, overflowY: 'auto', paddingRight: 4 }}>
            {isLoading ? (
              <div style={{ textAlign: 'center', marginTop: 80 }}><Spin size="large" /></div>
            ) : ticketTypes.length === 0 ? (
              <Empty 
                description="Không tìm thấy loại vé nào phù hợp" 
                image={Empty.PRESENTED_IMAGE_SIMPLE} 
                style={{ marginTop: 60 }} 
              />
            ) : (
              <Row gutter={[16, 16]}>
                {ticketTypes.map(tt => {
                  const isVeLuot = tt.ticketCategory === 'VE_LUOT'
                  const inCartItem = cart.find(item => item.ticketType.id === tt.id)

                  return (
                    <Col xs={24} sm={12} xl={8} key={tt.id}>
                      <Card
                        hoverable
                        onClick={() => handleSelectTicket(tt)}
                        style={{
                          borderColor: inCartItem ? '#0068ff' : '#e2e8f0',
                          borderRadius: 14,
                          transition: 'all 0.25s',
                          position: 'relative',
                          overflow: 'hidden',
                          boxShadow: inCartItem ? '0 4px 16px rgba(0, 104, 255, 0.12)' : '0 2px 6px rgba(0,0,0,0.03)',
                          background: inCartItem ? '#f8faff' : '#ffffff',
                          display: 'flex',
                          flexDirection: 'column',
                          justifyContent: 'space-between',
                          height: '100%',
                        }}
                        bodyStyle={{ padding: 16 }}
                      >
                        {/* Accent Bar on top */}
                        <div
                          style={{
                            position: 'absolute',
                            top: 0,
                            left: 0,
                            right: 0,
                            height: 4,
                            background: isVeLuot
                              ? 'linear-gradient(90deg, #0d9488, #2dd4bf)'
                              : 'linear-gradient(90deg, #0284c7, #38bdf8)',
                          }}
                        />

                        <div>
                          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
                            <Tag
                              color={isVeLuot ? 'cyan' : 'blue'}
                              style={{ borderRadius: 10, fontSize: 11, fontWeight: 600, border: 'none', margin: 0 }}
                            >
                              {isVeLuot ? 'Vé lượt' : 'Vé tháng'}
                            </Tag>
                            {inCartItem && (
                              <span style={{ fontSize: 11, color: '#0068ff', fontWeight: 700, background: '#e0edff', padding: '1px 8px', borderRadius: 10 }}>
                                Trong giỏ: x{inCartItem.quantity}
                              </span>
                            )}
                          </div>

                          <div style={{ fontWeight: 700, fontSize: 15, marginBottom: 8, color: '#1a1a2e', minHeight: 40, lineHeight: 1.35 }}>
                            {tt.name}
                          </div>
                        </div>

                        <div style={{ display: 'flex', alignItems: 'baseline', gap: 4, marginTop: 8 }}>
                          <span style={{ color: '#0068ff', fontWeight: 800, fontSize: 18 }}>
                            {new Intl.NumberFormat('vi-VN').format(tt.price)} đ
                          </span>
                          <span style={{ fontSize: 12, color: '#64748b', fontWeight: 500 }}>
                            {isVeLuot ? '/ lượt' : '/ 30 ngày'}
                          </span>
                        </div>
                      </Card>
                    </Col>
                  )
                })}
              </Row>
            )}
          </div>

          {/* Pagination */}
          {totalCount > 0 && (
            <div style={{ display: 'flex', justifyContent: 'center', marginTop: 14, paddingTop: 12, borderTop: '1px solid #f0f0f0', flexWrap: 'wrap', gap: 8 }}>
              <Pagination
                current={pageIndex}
                pageSize={pageSize}
                total={totalCount}
                onChange={(page, size) => {
                  setPageIndex(page)
                  if (size !== pageSize) {
                    setPageSize(size)
                    setPageIndex(1)
                  }
                }}
                showSizeChanger
                pageSizeOptions={['6', '9', '12', '18']}
                showTotal={(total, range) => `${range[0]}-${range[1]} / ${total} loại vé`}
                size="small"
              />
            </div>
          )}
        </div>

        {/* Right Column: Cart / Checkout */}
        <div className="pos-checkout">
          <Title level={4} style={{ borderBottom: '1px solid #f0f0f0', paddingBottom: 16, marginBottom: 16 }}>
            <ShoppingCartOutlined /> Giỏ Hàng
          </Title>
          
          <div style={{ flex: 1, overflowY: 'auto' }}>
            {cart.length === 0 ? (
              <div style={{ textAlign: 'center', color: '#8c8c8c', marginTop: 60 }}>
                Giỏ hàng trống
              </div>
            ) : (
              <List
                dataSource={cart}
                renderItem={(item) => {
                  const isVeLuot = item.ticketType.ticketCategory === 'VE_LUOT'
                  const entries = item.requestedEntries || 1
                  const itemUnitPrice = item.ticketType.price * (isVeLuot ? entries : 1)
                  const itemSubtotal = itemUnitPrice * item.quantity

                  return (
                    <List.Item
                      actions={[
                        <Button type="text" danger icon={<DeleteOutlined />} onClick={() => removeItem(item.ticketType.id)} />
                      ]}
                      style={{ display: 'block', padding: '12px 0' }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 8 }}>
                        <div>
                          <div style={{ fontWeight: 600, fontSize: 14 }}>{item.ticketType.name}</div>
                          <div style={{ color: '#0068ff', fontWeight: 700, fontSize: 14 }}>
                            {new Intl.NumberFormat('vi-VN').format(itemSubtotal)} đ
                            {isVeLuot && entries > 1 && (
                              <span style={{ fontSize: 11, color: '#64748b', fontWeight: 500, marginLeft: 6 }}>
                                ({new Intl.NumberFormat('vi-VN').format(item.ticketType.price)}đ × {entries} lượt)
                              </span>
                            )}
                          </div>
                        </div>
                      </div>

                      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', background: '#f8fafc', padding: '6px 10px', borderRadius: 8 }}>
                        {isVeLuot ? (
                          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                            <span style={{ fontSize: 12, color: '#0f766e', fontWeight: 600 }}>Số lượt:</span>
                            <InputNumber 
                              min={1} 
                              max={999} 
                              value={entries} 
                              onChange={v => updateRequestedEntries(item.ticketType.id, v || 1)} 
                              size="small"
                              style={{ width: 68 }}
                            />
                            <span style={{ fontSize: 11, color: entries > 1 ? '#0d9488' : '#8c8c8c' }}>
                              {entries > 1 ? '✓ Nhiều lượt' : '• 24h'}
                            </span>
                          </div>
                        ) : (
                          <div style={{ fontSize: 12, color: '#1e40af', fontWeight: 600 }}>
                            Vé tháng (30 ngày)
                          </div>
                        )}

                        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                          <span style={{ fontSize: 12, color: '#475569', fontWeight: 600 }}>Số vé:</span>
                          <InputNumber 
                            min={1} 
                            max={20} 
                            value={item.quantity} 
                            onChange={v => updateQuantity(item.ticketType.id, v || 1)} 
                            size="small"
                            style={{ width: 60 }}
                          />
                        </div>
                      </div>
                    </List.Item>
                  )
                }}
              />
            )}

            {requiresRegistration && (
              <div style={{ marginTop: 20, padding: 14, background: '#f0fdf4', borderRadius: 8, border: '1px solid #bbf7d0' }}>
                <div style={{ fontSize: 12, color: '#166534', fontWeight: 600, marginBottom: 4 }}>
                 Bắt buộc nhập SĐT khách hàng
                </div>
                <div style={{ fontSize: 11, color: '#15803d', marginBottom: 10 }}>
                  Đơn hàng có Vé tháng hoặc Vé lượt nhiều lần (&gt;1 lượt) cần số điện thoại để tạo/gắn tài khoản lưu trữ vé.
                </div>
                <Form layout="vertical">
                  <Form.Item 
                    label="Số điện thoại khách hàng" 
                    required 
                    validateStatus={customerPhone && !isValidPhone ? 'error' : ''}
                    help={customerPhone && !isValidPhone ? 'Số điện thoại không hợp lệ (phải bắt đầu bằng số 0 và gồm 10 chữ số)' : ''}
                    style={{ margin: 0 }}
                  >
                    <Input 
                      prefix={<PhoneOutlined />} 
                      placeholder="VD: 0912345678" 
                      value={customerPhone}
                      onChange={e => {
                        const val = e.target.value.replace(/\D/g, '');
                        setCustomerPhone(val);
                      }}
                      maxLength={10}
                    />
                  </Form.Item>     
                </Form>
              </div>
            )}
          </div>

          <div style={{ marginTop: 16 }}>
            <Divider style={{ margin: '12px 0' }} />

            {/* === KHU VỰC ÁP MÃ GIẢM GIÁ === */}
            <div style={{ marginBottom: 14 }}>
              {!appliedVoucher || appliedVoucherError ? (
                <div>
                  <div style={{ display: 'flex', gap: 8 }}>
                    <Select
                      showSearch
                      allowClear
                      loading={vouchersLoading}
                      placeholder="Chọn mã giảm giá..."
                      notFoundContent="Hiện không có mã giảm giá nào"
                      optionFilterProp="label"
                      value={appliedVoucherError ? null : voucherCode || null}
                      onChange={val => { setVoucherCode(val || ''); setVoucherError(''); setAppliedVoucher(null) }}
                      style={{ flex: 1, minWidth: 0 }}
                      disabled={cart.length === 0}
                      options={validVouchers.map(v => ({
                        value: v.code,
                        label: `${v.code} - Giảm ${v.discountType === 'PERCENTAGE' ? v.discountValue + '%' : new Intl.NumberFormat('vi-VN').format(v.discountValue) + 'đ'}`
                      }))}
                    />
                    <Button
                      type="primary"
                      ghost
                      onClick={handleApplyVoucher}
                      disabled={!voucherCode.trim() || cart.length === 0}
                      style={{ borderRadius: 8, flexShrink: 0 }}
                    >
                      Áp mã
                    </Button>
                  </div>
                  {(voucherError || appliedVoucherError) && (
                    <div style={{ marginTop: 6, color: '#e31748', fontSize: 12, display: 'flex', alignItems: 'center', gap: 4 }}>
                      <CloseCircleOutlined /> {voucherError || appliedVoucherError}
                    </div>
                  )}
                </div>
              ) : (
                <div style={{
                  background: 'linear-gradient(135deg, #f6ffed 0%, #e6fffb 100%)',
                  border: '1px dashed #03ca77',
                  borderRadius: 10, padding: '10px 14px',
                  display: 'flex', justifyContent: 'space-between', alignItems: 'center'
                }}>
                  <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 2 }}>
                      <CheckCircleOutlined style={{ color: '#03ca77' }} />
                      <span style={{ fontFamily: 'monospace', fontWeight: 700, color: '#135200', fontSize: 14 }}>
                        {appliedVoucher.code}
                      </span>
                      <Tag color="success" bordered={false} style={{ fontSize: 11 }}>ĐÃ ÁP</Tag>
                    </div>
                    <div style={{ fontSize: 12, color: '#03ca77', fontWeight: 600 }}>
                      Giảm {appliedVoucher.discountType === 'PERCENTAGE'
                        ? `${appliedVoucher.discountValue}%`
                        : `${new Intl.NumberFormat('vi-VN').format(appliedVoucher.discountValue)}đ`
                      }
                      {' · Tiết kiệm: '}
                      <strong style={{ color: '#e31748' }}>
                        -{new Intl.NumberFormat('vi-VN').format(discountAmount)}đ
                      </strong>
                    </div>
                  </div>
                  <Button
                    type="text"
                    danger
                    size="small"
                    icon={<CloseCircleOutlined />}
                    onClick={handleRemoveVoucher}
                  >
                    Xóa
                  </Button>
                </div>
              )}
            </div>

            {/* === GIÁ TỔNG === */}
            {appliedVoucher && discountAmount > 0 && (
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4, color: '#8c8c8c', fontSize: 14 }}>
                <span>Tạm tính:</span>
                <span>{new Intl.NumberFormat('vi-VN').format(totalAmount)}đ</span>
              </div>
            )}
            {appliedVoucher && discountAmount > 0 && (
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 8, color: '#03ca77', fontSize: 14 }}>
                <span>Giảm giá:</span>
                <span>-{new Intl.NumberFormat('vi-VN').format(discountAmount)}đ</span>
              </div>
            )}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
              <span style={{ fontSize: 16, fontWeight: 600 }}>TỔNG TIỀN:</span>
              <span style={{ fontSize: 24, fontWeight: 800, color: '#e31748' }}>
                {formattedTotal}
              </span>
            </div>

            <Button
              type="primary"
              size="large"
              disabled={!canCheckout}
              onClick={handleOpenPaymentMethod}
              style={{ width: '100%', height: 60, fontSize: 18, fontWeight: 700, borderRadius: 12, background: 'linear-gradient(135deg, #001f3e 0%, #002d59 60%, #0068ff 100%)', border: 'none' }}
            >
              THANH TOÁN
            </Button>
          </div>

        </div>
      </div>

      {/* Payment Method Modal */}
      <Modal
        title={<span style={{ fontSize: 20 }}>Chọn phương thức thanh toán</span>}
        open={paymentMethodVisible}
        onCancel={() => setPaymentMethodVisible(false)}
        footer={null}
        centered
        width={500}
      >
        <div style={{ textAlign: 'center', padding: '20px 0' }}>
          {appliedVoucher && discountAmount > 0 ? (
            <div style={{ marginBottom: 24, textAlign: 'center' }}>
              <p style={{ fontSize: 15, color: '#8c8c8c', marginBottom: 4 }}>
                Tạm tính: <span style={{ textDecoration: 'line-through' }}>{new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(totalAmount)}</span>
              </p>
              <p style={{ fontSize: 14, color: '#03ca77', marginBottom: 4 }}>
                Mã <strong>{appliedVoucher.code}</strong>: -{new Intl.NumberFormat('vi-VN').format(discountAmount)}đ
              </p>
              <p style={{ fontSize: 18, marginBottom: 0 }}>
                Thanh toán: <strong style={{ color: '#e31748', fontSize: 26 }}>{formattedTotal}</strong>
              </p>
            </div>
          ) : (
            <p style={{ fontSize: 18, marginBottom: 24 }}>Tổng số tiền cần thanh toán: <strong style={{ color: '#e31748', fontSize: 24 }}>{formattedTotal}</strong></p>
          )}
          <Space direction="vertical" style={{ width: '100%' }} size="large">
            <Button 
              type="primary" 
              size="large" 
              block 
              icon={<DollarOutlined />} 
              style={{ height: 60, fontSize: 18, background: '#03ca77', borderColor: '#03ca77' }}
              onClick={handlePayCash}
              loading={isSellingCash}
            >
              TIỀN MẶT (In vé ngay)
            </Button>
            <Button 
              type="primary" 
              size="large" 
              block 
              icon={<BankOutlined />} 
              style={{ height: 60, fontSize: 18 }}
              onClick={handlePayTransfer}
              loading={isCreatingOrder}
            >
              CHUYỂN KHOẢN (Quét VietQR)
            </Button>
          </Space>
        </div>
      </Modal>

      {/* VietQR Modal */}
      <Modal
        title={<span style={{ fontSize: 20 }}>Quét mã QR để thanh toán</span>}
        open={qrModalVisible}
        onCancel={() => {
          try { sessionStorage.removeItem('pos_pending_order'); } catch { /* ignore */ }
          setQrModalVisible(false);
          setPendingOrderId(null);
        }}
        footer={null}
        centered
        width={400}
        maskClosable={false}
      >
        <div style={{ textAlign: 'center' }}>
          <p style={{ fontSize: 16 }}>Vui lòng yêu cầu khách quét mã bên dưới để thanh toán.</p>

          {/* Cảnh báo chuyển khoản thiếu */}
          {orderStatusData?.status === 'PARTIAL' && (
            <div style={{
              background: '#fff7e6',
              border: '1px solid #ffa940',
              borderRadius: 8,
              padding: '10px 14px',
              marginBottom: 14,
              textAlign: 'left'
            }}>
              <div style={{ fontWeight: 700, color: '#d46b08', marginBottom: 4 }}>⚠️ Khách chuyển thiếu tiền!</div>
              <div style={{ fontSize: 13, color: '#555' }}>
                Đã nhận: <strong>{new Intl.NumberFormat('vi-VN').format(orderStatusData.paidAmount)} đ</strong>
              </div>
              <div style={{ fontSize: 13, color: '#555' }}>
                Còn thiếu: <strong style={{ color: '#faa828' }}>{new Intl.NumberFormat('vi-VN').format(orderStatusData.remainingAmount)} đ</strong>
              </div>
              <div style={{ fontSize: 12, color: '#888', marginTop: 4 }}>
                Mã QR bên dưới đã tự cập nhật số tiền còn thiếu để khách quét nốt.
              </div>
            </div>
          )}

          <div style={{ background: '#f0f2f5', padding: 16, borderRadius: 12, display: 'inline-block', marginBottom: 16 }}>
            {vietQRUrl ? (
              <img src={vietQRUrl} alt="VietQR" style={{ width: '100%', maxWidth: 300, borderRadius: 8 }} />
            ) : (
              <Spin size="large" />
            )}
          </div>
          <div style={{ marginBottom: 12 }}>
            <span style={{ fontSize: 15, display: 'block' }}>
              Số tiền: <strong style={{ color: '#faa828', fontSize: 16 }}>{new Intl.NumberFormat('vi-VN').format(currentQrAmount)} đ</strong>
            </span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#0068ff', fontSize: 16, fontWeight: 'bold' }}>
            <LoadingOutlined style={{ marginRight: 8 }} /> Đang chờ thanh toán...
          </div>
          <p style={{ color: '#8c8c8c', marginTop: 12, fontSize: 12 }}>
            Hệ thống sẽ tự động in vé ngay khi nhận đủ tiền.<br/>Mã đơn hàng: {transactionRef}
          </p>
          <div style={{ marginTop: 10, padding: '6px 12px', background: '#eff6ff', borderRadius: 6, color: '#1e40af', fontSize: 12, display: 'inline-block' }}>
            ⏱️ Đơn hàng sẽ tự động hủy sau 5 phút nếu không hoàn tất thanh toán.
          </div>
        </div>
      </Modal>

      {/* --- PHẦN TỬ CHỈ HIỆN KHI IN (Được inject thẳng vào thẻ body) --- */}
      {receiptVisible && createPortal(
        <div className="print-section" style={{ padding: 20, fontFamily: 'monospace', maxWidth: 350, margin: '0 auto' }}>
          <div style={{ textAlign: 'center', marginBottom: 16 }}>
            <h2 style={{ margin: 0, fontSize: 22 }}>THÀNH MINH POOL</h2>
            <p style={{ margin: 0, fontSize: 14 }}>Biên lai bán vé tại quầy</p>
            <p style={{ margin: 0, fontSize: 14 }}>Ngày: {new Date().toLocaleString('vi-VN')}</p>
          </div>
          
          <Divider style={{ margin: '12px 0', borderColor: '#000', borderStyle: 'dashed' }} />
          
          <div style={{ marginBottom: 16, fontSize: 14 }}>
            <div style={{ marginBottom: 8 }}>Chi tiết vé:</div>
            <div style={{ marginBottom: 12 }}>{soldOrder?.cartSummary}</div>
            
            <div style={{ marginTop: 8 }}>
              Tạm tính: {soldOrder ? new Intl.NumberFormat('vi-VN').format(soldOrder.totalAmount) : 0} đ
            </div>
            {soldOrder?.discountAmount > 0 && (
              <div style={{ marginTop: 4, color: '#e31748' }}>
                Mã giảm giá ({soldOrder?.voucherCode}): -{new Intl.NumberFormat('vi-VN').format(soldOrder.discountAmount)} đ
              </div>
            )}
            <div style={{ marginTop: 4, fontWeight: 'bold', fontSize: 16 }}>
              Tổng thanh toán: {soldOrder ? new Intl.NumberFormat('vi-VN').format(soldOrder.finalAmount) : 0} đ
            </div>
          </div>
          
          <Divider style={{ margin: '12px 0', borderColor: '#000', borderStyle: 'dashed' }} />
          
          {/* IN THẺ CHO VÉ THÁNG / VÉ LƯỢT */}
          {soldTickets.map(t => (
            <div key={t.id} style={{ marginBottom: 24, pageBreakInside: 'avoid', border: '2px solid #000', padding: 12, borderRadius: 8 }}>
              {/* MẶT TRƯỚC (Thông tin thẻ) */}
              <div style={{ textAlign: 'center', borderBottom: '1px dashed #000', paddingBottom: 8, marginBottom: 8 }}>
                <h3 style={{ margin: 0, fontSize: 18, textTransform: 'uppercase', color: '#d81b60' }}>THÀNH MINH POOL</h3>
                <h2 style={{ margin: '4px 0', fontSize: 20, backgroundColor: '#d81b60', color: 'black', padding: '4px 0', WebkitPrintColorAdjust: 'exact', printColorAdjust: 'exact' }}>
                  {t.ticketCategory === 'VE_THANG' ? 'THẺ VÉ THÁNG' : t.ticketCategory === 'VE_LUOT' ? 'THẺ VÉ LƯỢT' : t.ticketCategory}
                </h2>
                <div style={{ textAlign: 'left', fontSize: 13, marginTop: 8 }}>
                  <p style={{ margin: '4px 0' }}><strong>Khách hàng:</strong> {soldOrder?.customerName || '....................'}</p>
                  <p style={{ margin: '4px 0' }}><strong>SĐT:</strong> {soldOrder?.customerPhone || '....................'}</p>
                  <p style={{ margin: '4px 0' }}>
                    <strong>Hạn dùng:</strong> {new Date().toLocaleDateString('vi-VN')} - {t.expiryDate ? new Date(t.expiryDate).toLocaleDateString('vi-VN') : 'Không thời hạn'}
                  </p>
                  {t.remainingEntries !== undefined && t.remainingEntries !== null && (
                    <p style={{ margin: '4px 0' }}><strong>Số lượt:</strong> {t.remainingEntries} lượt</p>
                  )}
                </div>
              </div>

              {/* MẶT SAU (Mã QR + Chú ý) */}
              <div style={{ textAlign: 'center' }}>
                <p style={{ margin: '0 0 8px 0', fontSize: 14, fontWeight: 'bold' }}>MÃ QUÉT VÀO CỔNG</p>
                <QRCodeSVG value={t.qrCode} size={150} style={{ margin: '0 auto' }} />
                <p style={{ margin: '4px 0 12px 0', fontSize: 11 }}>{t.qrCode}</p>
                
                <div style={{ textAlign: 'left', fontSize: 12, borderTop: '1px solid #000', paddingTop: 8 }}>
                  <p style={{ margin: '0 0 4px 0', color: 'red', fontWeight: 'bold', textDecoration: 'underline' }}>CHÚ Ý:</p>
                  <ul style={{ margin: 0, paddingLeft: 16 }}>
                    <li style={{ marginBottom: 4 }}>Sử dụng thẻ đúng tên chủ thẻ.</li>
                    <li style={{ marginBottom: 4 }}>Tuân thủ các quy định của bể bơi.</li>
                    {t.ticketCategory === 'VE_THANG' && (
                      <li>Chủ thẻ chỉ sử dụng tối đa 1 lần/ngày.</li>
                    )}
                  </ul>
                </div>
              </div>
            </div>
          ))}

          {generatedAccount && (
            <div style={{ marginTop: 16, padding: '8px', border: '1px solid #000', fontSize: 11 }}>
              <p style={{ margin: '0 0 4px 0', fontWeight: 'bold' }}>TÀI KHOẢN KHÁCH HÀNG (Dùng để đăng nhập Web):</p>
              <p style={{ margin: 0 }}>SĐT: {generatedAccount.username}</p>
              <p style={{ margin: 0 }}>Mật khẩu: {generatedAccount.password}</p>
              <Divider style={{ margin: '16px 0', borderColor: '#000', borderStyle: 'dashed' }} />
            </div>
          )}
          
          <p style={{ textAlign: 'center', fontSize: 14 }}>
            Cảm ơn quý khách!<br/>Chúc quý khách bơi lội vui vẻ.
          </p>
        </div>,
        document.body
      )}

      {/* Success Modal (Cho nhân viên thấy) */}
      <Modal
        open={receiptVisible}
        footer={null}
        closable={false}
        centered
        width={400}
      >
        <Result
          icon={<CheckCircleFilled style={{ color: '#03ca77' }} />}
          title="Bán vé thành công!"
          subTitle={`Tổng tiền: ${soldOrder ? new Intl.NumberFormat('vi-VN').format(soldOrder.totalAmount) : 0} đ`}
          extra={[
            <Button key="print" type="primary" icon={<PrinterOutlined />} onClick={handlePrint} size="large" style={{ width: '100%', marginBottom: 12 }}>
              In Vé Ngay
            </Button>,
            <Button key="close" onClick={() => setReceiptVisible(false)} style={{ width: '100%' }}>
              Đóng & Bán tiếp
            </Button>
          ]}
        />
      </Modal>
    </>
  )
}


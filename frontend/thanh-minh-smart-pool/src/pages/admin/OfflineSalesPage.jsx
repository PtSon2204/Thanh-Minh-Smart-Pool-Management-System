import { useState, useEffect } from 'react'
import { createPortal } from 'react-dom'
import { Button, Row, Col, Typography, Card, InputNumber, Modal, Spin, Result, Divider, List, Input, Form, Space, message, DatePicker, Tag, Select } from 'antd'
import { ShoppingCartOutlined, PrinterOutlined, CheckCircleFilled, DeleteOutlined, UserOutlined, PhoneOutlined, BankOutlined, DollarOutlined, LoadingOutlined, CloseCircleOutlined, CheckCircleOutlined } from '@ant-design/icons'
import { QRCodeSVG } from 'qrcode.react'
import dayjs from 'dayjs'
import { useTicketTypes } from '../../features/tickets/hooks/useTicketTypes'
import { useSellOfflineTicket, useCreatePendingOrder, useOrderStatus } from '../../features/tickets/hooks/useSellOfflineTicket'
import { useVouchers } from '../../features/vouchers/hooks/useVouchers'


const { Title } = Typography

export default function OfflineSalesPage() {
  const [cart, setCart] = useState([])
  const [customerPhone, setCustomerPhone] = useState('')
  const [customerName, setCustomerName] = useState('')
  const [startDate, setStartDate] = useState(dayjs())

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

  const { data: pagedData, isLoading } = useTicketTypes({
    isActive: true,
    pageIndex: 1,
    pageSize: 100,
  })

  const { data: voucherData, isLoading: vouchersLoading } = useVouchers({
    isActive: true,
    pageIndex: 1,
    pageSize: 100,
  })
  const availableVouchers = voucherData?.items || []
  
  const ticketTypes = pagedData?.items || []
  
  // Grouping theo loại vé
  const veThang = ticketTypes.filter(t => t.ticketCategory === 'VE_THANG')
  const veLuot = ticketTypes.filter(t => t.ticketCategory === 'VE_LUOT')

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

  // Watch for order completion
  useEffect(() => {
    if (orderStatusData && orderStatusData.status === 'COMPLETED') {
      message.success('Đã nhận được tiền thanh toán!');
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setSoldTickets(orderStatusData.tickets || []);
      setQrModalVisible(false);
      setPendingOrderId(null);
      setReceiptVisible(true);
      setCart([]);
      setCustomerName('');
      setCustomerPhone('');
      setStartDate(dayjs());
    }
  }, [orderStatusData])

  const handleSelectTicket = (tt) => {
    setCart((prev) => {
      const existing = prev.find(item => item.ticketType.id === tt.id)
      if (existing) {
        return prev.map(item => item.ticketType.id === tt.id ? { ...item, quantity: item.quantity + 1 } : item)
      }
      return [...prev, { ticketType: tt, quantity: 1 }]
    })
  }

  const updateQuantity = (id, q) => {
    if (q < 1) return
    setCart(prev => prev.map(item => item.ticketType.id === id ? { ...item, quantity: q } : item))
  }

  const removeItem = (id) => {
    setCart(prev => prev.filter(item => item.ticketType.id !== id))
  }

  const totalAmount = cart.reduce((sum, item) => sum + (item.ticketType.price * item.quantity), 0)

  // Tính discount theo Snapshot tại thời điểm áp mã
  const discountAmount = (() => {
    if (!appliedVoucher) return 0
    if (appliedVoucher.minOrderValue && totalAmount < appliedVoucher.minOrderValue) return 0
    if (appliedVoucher.discountType === 'PERCENTAGE') {
      return Math.round(totalAmount * appliedVoucher.discountValue / 100)
    }
    return Math.min(appliedVoucher.discountValue, totalAmount)
  })()

  const finalAmount = totalAmount - discountAmount
  const formattedTotal = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(finalAmount)

  const handleApplyVoucher = async () => {
    const code = voucherCode.trim().toUpperCase()
    if (!code) return
    setVoucherError('')
    try {
      const matched = availableVouchers.find(v => v.code === code)
      if (!matched) {
        setVoucherError('Mã giảm giá không tồn tại hoặc đã bị khóa.')
        setAppliedVoucher(null)
        return
      }
      // Kiểm tra hạn sử dụng
      const now = new Date()
      if (matched.startDate && new Date(matched.startDate) > now) {
        setVoucherError('Mã giảm giá chưa đến thời gian sử dụng.')
        setAppliedVoucher(null)
        return
      }
      if (matched.endDate && new Date(matched.endDate) < now) {
        setVoucherError('Mã giảm giá đã hết hạn.')
        setAppliedVoucher(null)
        return
      }
      // Kiểm tra đơn tối thiểu
      if (matched.minOrderValue && totalAmount < matched.minOrderValue) {
        setVoucherError(`Đơn hàng tối thiểu ${new Intl.NumberFormat('vi-VN').format(matched.minOrderValue)}đ để dùng mã này.`)
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


  // VE_THANG: cần đăng ký TK để gắn QR + StartDate
  // VE_LUOT tại quầy: không cần đăng ký (trừ lượt ngay, không QR)
  const requiresRegistration = cart.some(item => item.ticketType.ticketCategory === 'VE_THANG')
  const requiresStartDate = requiresRegistration

  const isValidPhone = customerPhone ? /^0\d{9}$/.test(customerPhone.trim()) : false;
  const canCheckout = cart.length > 0 && (!requiresRegistration || isValidPhone)

  const handleOpenPaymentMethod = () => {
    if (!canCheckout) return
    setPaymentMethodVisible(true)
  }

  const getItemsPayload = () => cart.map(item => ({
    ticketTypeId: item.ticketType.id,
    quantity: item.quantity
  }))

  const getOrderSummary = () => cart.map(c => `${c.ticketType.name} x${c.quantity}`).join(', ')

  const handlePayCash = () => {
    const payload = { 
      items: getItemsPayload(),
      customerPhone: requiresRegistration ? customerPhone.trim() : null,
      customerName: requiresRegistration ? customerName.trim() : null,
      startDate: requiresRegistration ? startDate.toISOString() : null,
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
            customerName: requiresRegistration ? customerName.trim() : '',
            customerPhone: requiresRegistration ? customerPhone.trim() : ''
          })
          setSoldTickets(data.tickets)
          setGeneratedAccount(data.accountInfo || null)
          setPaymentMethodVisible(false)
          setReceiptVisible(true)
          setCart([])
          setCustomerName('')
          setCustomerPhone('')
          setStartDate(dayjs())
          setAppliedVoucher(null)
          setVoucherCode('')
        }
      }
    )
  }

  const handlePayTransfer = () => {
    const payload = { 
      items: getItemsPayload(),
      customerPhone: requiresRegistration ? customerPhone.trim() : null,
      customerName: requiresRegistration ? customerName.trim() : null,
      startDate: requiresRegistration ? startDate.toISOString() : null,
      voucherCode: appliedVoucher ? appliedVoucher.code : null
    }

    createPendingOrder(
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
            customerName: requiresRegistration ? customerName.trim() : '',
            customerPhone: requiresRegistration ? customerPhone.trim() : ''
          })
          setGeneratedAccount(data.accountInfo || null)
          setTransactionRef(data.transactionRef)
          setPendingOrderId(data.orderId)
          
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

  const renderTicketSection = (title, tickets) => (
    tickets.length > 0 && (
      <div style={{ marginBottom: 24 }}>
        <Title level={5} style={{ color: '#005f8e', borderBottom: '2px solid #005f8e', paddingBottom: 8, display: 'inline-block' }}>
          {title}
        </Title>
        <Row gutter={[16, 16]} style={{ marginTop: 12 }}>
          {tickets.map(tt => (
            <Col span={8} key={tt.id}>
              <Card
                hoverable
                onClick={() => handleSelectTicket(tt)}
                style={{
                  borderColor: '#f0f0f0',
                  textAlign: 'center',
                  borderRadius: 12,
                  transition: 'all 0.2s'
                }}
                bodyStyle={{ padding: 16 }}
              >
                <div style={{ fontWeight: 600, fontSize: 16, marginBottom: 8, color: '#1a1a2e' }}>
                  {tt.name}
                </div>
                <div style={{ color: '#005f8e', fontWeight: 700, fontSize: 18 }}>
                  {new Intl.NumberFormat('vi-VN').format(tt.price)} đ
                </div>
              </Card>
            </Col>
          ))}
        </Row>
      </div>
    )
  )

  const bankId = import.meta.env.VITE_VIETQR_BANK_ID || '970422';
  const accNo = import.meta.env.VITE_VIETQR_ACCOUNT_NO || '';
  const accName = import.meta.env.VITE_VIETQR_ACCOUNT_NAME || 'PHAM THE SON';
  
  const vietQRUrl = transactionRef 
    ? `https://img.vietqr.io/image/${bankId}-${accNo}-compact2.png?amount=${finalAmount}&addInfo=${transactionRef}&accountName=${encodeURIComponent(accName)}` 
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

      <div className="pos-container" style={{ height: 'calc(100vh - 120px)', display: 'flex', gap: 24 }}>
        
        {/* Left Column: Ticket Types Menu */}
        <div style={{ flex: 2, display: 'flex', flexDirection: 'column' }}>
          <Title level={4} style={{ marginBottom: 16 }}>🎯 Màn hình bán vé (POS)</Title>
          <div style={{ flex: 1, overflowY: 'auto', paddingRight: 8 }}>
            {isLoading ? (
              <div style={{ textAlign: 'center', marginTop: 100 }}><Spin size="large" /></div>
            ) : (
              <div>
                {renderTicketSection('📅 Vé tháng (Có QR, 1 lượt/ngày)', veThang)}
                {renderTicketSection('🎫 Vé lượt (Mua tại quầy, bơi ngay)', veLuot)}
              </div>
            )}
          </div>
        </div>

        {/* Right Column: Cart / Checkout */}
        <div style={{ flex: 1, background: '#fff', borderRadius: 16, padding: 24, boxShadow: '0 4px 20px rgba(0,0,0,0.05)', display: 'flex', flexDirection: 'column' }}>
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
                renderItem={(item) => (
                  <List.Item
                    actions={[
                      <Button type="text" danger icon={<DeleteOutlined />} onClick={() => removeItem(item.ticketType.id)} />
                    ]}
                  >
                    <List.Item.Meta
                      title={<span style={{ fontWeight: 600 }}>{item.ticketType.name}</span>}
                      description={<span style={{ color: '#005f8e', fontWeight: 600 }}>{new Intl.NumberFormat('vi-VN').format(item.ticketType.price)} đ</span>}
                    />
                    <InputNumber 
                      min={1} 
                      max={20} 
                      value={item.quantity} 
                      onChange={v => updateQuantity(item.ticketType.id, v || 1)} 
                    />
                  </List.Item>
                )}
              />
            )}

            {requiresRegistration && (
              <div style={{ marginTop: 24, padding: 16, background: '#f8fafc', borderRadius: 8, border: '1px solid #e2e8f0' }}>
                <div style={{ fontWeight: 600, color: '#ff4d4f', marginBottom: 12 }}>
                  * Bắt buộc nhập thông tin khách hàng (do có mua Vé Tháng)
                </div>
                <Form layout="vertical">
                  <Form.Item 
                    label="Số điện thoại khách hàng" 
                    required 
                    validateStatus={customerPhone && !isValidPhone ? 'error' : ''}
                    help={customerPhone && !isValidPhone ? 'Số điện thoại không hợp lệ (phải bắt đầu bằng số 0 và gồm 10 chữ số)' : ''}
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
                  <Form.Item label="Tên khách hàng (Không bắt buộc)">
                    <Input 
                      prefix={<UserOutlined />} 
                      placeholder="Nhập tên..." 
                      value={customerName}
                      onChange={e => setCustomerName(e.target.value)}
                    />
                  </Form.Item>
                  {requiresStartDate && (
                    <Form.Item label="Ngày bắt đầu có hiệu lực">
                      <DatePicker 
                        format="DD/MM/YYYY" 
                        value={startDate} 
                        onChange={val => setStartDate(val || dayjs())}
                        allowClear={false}
                        style={{ width: '100%' }}
                      />
                    </Form.Item>
                  )}
                </Form>
              </div>
            )}
          </div>

          <div style={{ marginTop: 16 }}>
            <Divider style={{ margin: '12px 0' }} />

            {/* === KHU VỰC ÁP MÃ GIẢM GIÁ === */}
            <div style={{ marginBottom: 14 }}>
              {!appliedVoucher ? (
                <div>
                  <div style={{ display: 'flex', gap: 8 }}>
                    <Select
                      showSearch
                      allowClear
                      loading={vouchersLoading}
                      placeholder="Chọn mã giảm giá..."
                      optionFilterProp="label"
                      value={voucherCode || null}
                      onChange={val => { setVoucherCode(val || ''); setVoucherError('') }}
                      style={{ flex: 1, minWidth: 200 }}
                      disabled={cart.length === 0}
                      options={availableVouchers.map(v => ({
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
                  {voucherError && (
                    <div style={{ marginTop: 6, color: '#ff4d4f', fontSize: 12, display: 'flex', alignItems: 'center', gap: 4 }}>
                      <CloseCircleOutlined /> {voucherError}
                    </div>
                  )}
                </div>
              ) : (
                <div style={{
                  background: 'linear-gradient(135deg, #f6ffed 0%, #e6fffb 100%)',
                  border: '1px dashed #52c41a',
                  borderRadius: 10, padding: '10px 14px',
                  display: 'flex', justifyContent: 'space-between', alignItems: 'center'
                }}>
                  <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 2 }}>
                      <CheckCircleOutlined style={{ color: '#52c41a' }} />
                      <span style={{ fontFamily: 'monospace', fontWeight: 700, color: '#135200', fontSize: 14 }}>
                        {appliedVoucher.code}
                      </span>
                      <Tag color="success" bordered={false} style={{ fontSize: 11 }}>ĐÃ ÁP</Tag>
                    </div>
                    <div style={{ fontSize: 12, color: '#52c41a', fontWeight: 600 }}>
                      Giảm {appliedVoucher.discountType === 'PERCENTAGE'
                        ? `${appliedVoucher.discountValue}%`
                        : `${new Intl.NumberFormat('vi-VN').format(appliedVoucher.discountValue)}đ`
                      }
                      {' · Tiết kiệm: '}
                      <strong style={{ color: '#cf1322' }}>
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
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 8, color: '#52c41a', fontSize: 14 }}>
                <span>Giảm giá:</span>
                <span>-{new Intl.NumberFormat('vi-VN').format(discountAmount)}đ</span>
              </div>
            )}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
              <span style={{ fontSize: 16, fontWeight: 600 }}>TỔNG TIỀN:</span>
              <span style={{ fontSize: 24, fontWeight: 800, color: '#ff4d4f' }}>
                {formattedTotal}
              </span>
            </div>

            <Button
              type="primary"
              size="large"
              disabled={!canCheckout}
              onClick={handleOpenPaymentMethod}
              style={{ width: '100%', height: 60, fontSize: 18, fontWeight: 700, borderRadius: 12, background: 'linear-gradient(90deg, #005f8e 0%, #00b4d8 100%)', border: 'none' }}
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
              <p style={{ fontSize: 14, color: '#52c41a', marginBottom: 4 }}>
                Mã <strong>{appliedVoucher.code}</strong>: -{new Intl.NumberFormat('vi-VN').format(discountAmount)}đ
              </p>
              <p style={{ fontSize: 18, marginBottom: 0 }}>
                Thanh toán: <strong style={{ color: '#ff4d4f', fontSize: 26 }}>{formattedTotal}</strong>
              </p>
            </div>
          ) : (
            <p style={{ fontSize: 18, marginBottom: 24 }}>Tổng số tiền cần thanh toán: <strong style={{ color: '#ff4d4f', fontSize: 24 }}>{formattedTotal}</strong></p>
          )}
          <Space direction="vertical" style={{ width: '100%' }} size="large">
            <Button 
              type="primary" 
              size="large" 
              block 
              icon={<DollarOutlined />} 
              style={{ height: 60, fontSize: 18, background: '#52c41a', borderColor: '#52c41a' }}
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
          <div style={{ background: '#f0f2f5', padding: 16, borderRadius: 12, display: 'inline-block', marginBottom: 16 }}>
            {vietQRUrl ? (
              <img src={vietQRUrl} alt="VietQR" style={{ width: '100%', maxWidth: 300, borderRadius: 8 }} />
            ) : (
              <Spin size="large" />
            )}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#1890ff', fontSize: 16, fontWeight: 'bold' }}>
            <LoadingOutlined style={{ marginRight: 8 }} /> Đang chờ thanh toán...
          </div>
          <p style={{ color: '#8c8c8c', marginTop: 12, fontSize: 12 }}>
            Hệ thống sẽ tự động in vé ngay khi nhận được tiền.<br/>Mã đơn hàng: {transactionRef}
          </p>
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
              <div style={{ marginTop: 4, color: '#ff4d4f' }}>
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
          icon={<CheckCircleFilled style={{ color: '#52c41a' }} />}
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

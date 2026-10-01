import { useState, useEffect } from 'react'
import { createPortal } from 'react-dom'
import { Button, Row, Col, Typography, Card, InputNumber, Modal, Spin, Result, Divider, List, Input, Form, Space, message } from 'antd'
import { ShoppingCartOutlined, PrinterOutlined, CheckCircleFilled, DeleteOutlined, UserOutlined, PhoneOutlined, BankOutlined, DollarOutlined, LoadingOutlined } from '@ant-design/icons'
import { QRCodeSVG } from 'qrcode.react'
import { useTicketTypes } from '../../features/tickets/hooks/useTicketTypes'
import { useSellOfflineTicket, useCreatePendingOrder, useOrderStatus } from '../../features/tickets/hooks/useSellOfflineTicket'

const { Title } = Typography

export default function OfflineSalesPage() {
  const [cart, setCart] = useState([])
  const [customerPhone, setCustomerPhone] = useState('')
  const [customerName, setCustomerName] = useState('')

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
  
  const ticketTypes = pagedData?.items || []
  
  // Grouping
  const veThuong = ticketTypes.filter(t => t.ticketCategory === 'VE_THUONG')
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
  const formattedTotal = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(totalAmount)

  // Check if cart requires registration
  const requiresRegistration = cart.some(item => 
    item.ticketType.ticketCategory === 'VE_THANG' || item.ticketType.ticketCategory === 'VE_LUOT'
  )

  const canCheckout = cart.length > 0 && (!requiresRegistration || (customerPhone.trim() !== ''))

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
      customerPhone: requiresRegistration ? customerPhone : null,
      customerName: requiresRegistration ? customerName : null
    }

    sellTicket(
      payload,
      {
        onSuccess: (data) => {
          setSoldOrder({
            id: data.orderId,
            totalAmount: data.totalAmount,
            cartSummary: getOrderSummary(),
          })
          setSoldTickets(data.tickets)
          setGeneratedAccount(data.accountInfo || null)
          setPaymentMethodVisible(false)
          setReceiptVisible(true)
          setCart([])
          setCustomerName('')
          setCustomerPhone('')
        }
      }
    )
  }

  const handlePayTransfer = () => {
    const payload = { 
      items: getItemsPayload(),
      customerPhone: requiresRegistration ? customerPhone : null,
      customerName: requiresRegistration ? customerName : null
    }

    createPendingOrder(
      payload,
      {
        onSuccess: (data) => {
          setSoldOrder({
            id: data.orderId,
            totalAmount: data.totalAmount,
            cartSummary: getOrderSummary(),
          })
          setGeneratedAccount(data.accountInfo || null)
          setTransactionRef(data.transactionRef)
          setPendingOrderId(data.orderId)
          
          setPaymentMethodVisible(false)
          setQrModalVisible(true)
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
    ? `https://img.vietqr.io/image/${bankId}-${accNo}-compact2.png?amount=${totalAmount}&addInfo=${transactionRef}&accountName=${encodeURIComponent(accName)}` 
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
                {renderTicketSection('🎟️ Vé ngày (Bơi 1 lần)', veThuong)}
                {renderTicketSection('📅 Vé tháng', veThang)}
                {renderTicketSection('🎫 Vé lượt (Mua nhiều lượt)', veLuot)}
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
                  * Bắt buộc đăng ký tài khoản cho khách (do có mua Vé Tháng/Lượt)
                </div>
                <Form layout="vertical">
                  <Form.Item label="Số điện thoại khách hàng" required>
                    <Input 
                      prefix={<PhoneOutlined />} 
                      placeholder="Nhập SĐT..." 
                      value={customerPhone}
                      onChange={e => setCustomerPhone(e.target.value)}
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
                </Form>
              </div>
            )}
          </div>

          <div style={{ marginTop: 16 }}>
            <Divider style={{ margin: '12px 0' }} />
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
          <p style={{ fontSize: 18, marginBottom: 24 }}>Tổng số tiền cần thanh toán: <strong style={{ color: '#ff4d4f', fontSize: 24 }}>{formattedTotal}</strong></p>
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
            <p style={{ margin: 0, fontSize: 12 }}>Biên lai bán vé tại quầy</p>
            <p style={{ margin: 0, fontSize: 12 }}>Ngày: {new Date().toLocaleString('vi-VN')}</p>
          </div>
          
          <Divider style={{ margin: '12px 0', borderColor: '#000', borderStyle: 'dashed' }} />
          
          <div style={{ marginBottom: 16, fontSize: 13 }}>
            <div style={{ marginBottom: 8 }}><strong>Chi tiết vé:</strong></div>
            <div style={{ marginBottom: 12 }}>{soldOrder?.cartSummary}</div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 8, fontSize: 16 }}>
              <strong>Tổng cộng:</strong> <strong>{soldOrder ? new Intl.NumberFormat('vi-VN').format(soldOrder.totalAmount) : 0} đ</strong>
            </div>
          </div>
          
          <Divider style={{ margin: '12px 0', borderColor: '#000', borderStyle: 'dashed' }} />
          
          {soldTickets.map((t, idx) => (
            <div key={t.id} style={{ textAlign: 'center', marginBottom: 24, pageBreakInside: 'avoid' }}>
              <p style={{ margin: '0 0 8px 0', fontWeight: 'bold' }}>VÉ SỐ #{idx + 1}</p>
              <QRCodeSVG value={t.qrCode} size={200} style={{ margin: '0 auto' }} />
              <p style={{ margin: '8px 0 0 0', fontSize: 12 }}>Mã: {t.qrCode}</p>
              
              {t.ticketCategory === 'VE_THUONG' ? (
                <p style={{ margin: '4px 0 0 0', fontSize: 11 }}>HSD: 24h kể từ lúc in</p>
              ) : (
                <p style={{ margin: '4px 0 0 0', fontSize: 11, fontWeight: 'bold' }}>Vui lòng giữ lại mã QR này!</p>
              )}

              {idx < soldTickets.length - 1 && <Divider style={{ margin: '16px 0', borderColor: '#000', borderStyle: 'dashed' }} />}
            </div>
          ))}

          {generatedAccount && (
            <div style={{ marginTop: 16, padding: '8px', border: '1px solid #000', fontSize: 11 }}>
              <p style={{ margin: '0 0 4px 0', fontWeight: 'bold' }}>TÀI KHOẢN KHÁCH HÀNG (Dùng để đăng nhập Web):</p>
              <p style={{ margin: 0 }}>SĐT: {generatedAccount.username}</p>
              <p style={{ margin: 0 }}>Mật khẩu: {generatedAccount.password}</p>
            </div>
          )}
          
          <p style={{ textAlign: 'center', fontSize: 12, marginTop: 16 }}>
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

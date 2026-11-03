import { useState, useEffect } from 'react'
import {
  Table,
  Button,
  Space,
  Typography,
  Card,
  Input,
  Row,
  Col,
  Tooltip,
  Modal,
  Tag,
  message,
  Spin,
  Alert,
} from 'antd'
import {
  QrcodeOutlined,
  ReloadOutlined,
  SearchOutlined,
  ClockCircleOutlined,
  CheckCircleOutlined,
  LoadingOutlined,
  PrinterOutlined,
  CreditCardOutlined,
} from '@ant-design/icons'
import dayjs from 'dayjs'
import { usePayments } from '../../../features/payments/hooks/usePayments'
import { useOrderStatus } from '../../../features/tickets/hooks/useSellOfflineTicket'
import './WaitingInvoicesPage.css'

const { Title, Text } = Typography

const formatCurrency = (val) => {
  if (val === null || val === undefined) return '0 ₫'
  return new Intl.NumberFormat('vi-VN').format(val) + ' ₫'
}

export default function WaitingInvoicesPage() {
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [search, setSearch] = useState('')
  const [now, setNow] = useState(() => Date.now())

  // Modal tiếp tục thanh toán
  const [activeRecord, setActiveRecord] = useState(null)
  const [qrModalOpen, setQrModalOpen] = useState(false)
  const [paymentSuccessData, setPaymentSuccessData] = useState(null)

  // Real-time ticking for countdowns
  useEffect(() => {
    const timer = setInterval(() => {
      setNow(Date.now())
    }, 1000)
    return () => clearInterval(timer)
  }, [])

  const queryParams = {
    pageIndex,
    pageSize,
    status: 'PENDING',
    search: search.trim() || undefined,
  }

  const { data, isLoading, refetch } = usePayments(queryParams)

  // Polling order status khi modal QR đang mở
  const activeOrderId = activeRecord?.orderId
  const { data: orderStatusData } = useOrderStatus(activeOrderId, {
    refetchInterval: (query) => {
      if (!qrModalOpen || !activeOrderId) return false
      const currentStatus = query?.state?.data?.status
      if (currentStatus === 'COMPLETED' || currentStatus === 'CANCELLED' || currentStatus === 'FAILED') return false
      return 3000
    },
    enabled: Boolean(qrModalOpen && activeOrderId),
  })

  // Theo dõi khi đơn hàng hoàn thành hoặc bị hết hạn trong modal
  useEffect(() => {
    if (!qrModalOpen || !orderStatusData) return

    if (orderStatusData.status === 'COMPLETED') {
      message.success('Thanh toán thành công!')
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setPaymentSuccessData(orderStatusData)
      refetch()
    } else if (orderStatusData.status === 'CANCELLED' || orderStatusData.status === 'FAILED') {
      message.error('Giao dịch đã tự động hết hạn sau 5 phút.')
      setQrModalOpen(false)
      setActiveRecord(null)
      refetch()
    }
  }, [orderStatusData, qrModalOpen, refetch])


  // Xử lý mở modal tiếp tục thanh toán
  const handleOpenPayment = (record) => {
    setActiveRecord(record)
    setPaymentSuccessData(null)
    setQrModalOpen(true)
  }

  // Xử lý đóng modal
  const handleCloseModal = () => {
    setQrModalOpen(false)
    setActiveRecord(null)
    setPaymentSuccessData(null)
  }

  const bankId = import.meta.env.VITE_VIETQR_BANK_ID || '970422'
  const accNo = import.meta.env.VITE_VIETQR_ACCOUNT_NO || '1508200456788'
  const accName = import.meta.env.VITE_VIETQR_ACCOUNT_NAME || 'PHAM THE SON'

  // Tính số tiền QR hiện tại (nếu chuyển thiếu thì lấy remainingAmount)
  const qrAmount = (orderStatusData?.status === 'PARTIAL' && orderStatusData?.remainingAmount !== undefined)
    ? orderStatusData.remainingAmount
    : (activeRecord?.amount || 0)

  const activeTxRef = activeRecord?.transactionRef || ''
  const activeVietQRUrl = activeTxRef
    ? `https://img.vietqr.io/image/${bankId}-${accNo}-compact2.png?amount=${qrAmount}&addInfo=${activeTxRef}&accountName=${encodeURIComponent(accName)}`
    : ''

  // Tính countdown cho active record trong modal
  const activeCreatedMs = activeRecord
    ? (activeRecord.createdAt ? new Date(activeRecord.createdAt).getTime() : new Date(activeRecord.paymentTime).getTime())
    : 0
  const activeExpiresMs = activeRecord?.expiresAtUtc
    ? new Date(activeRecord.expiresAtUtc).getTime()
    : activeCreatedMs + 5 * 60 * 1000
  const activeSecondsLeft = Math.max(0, Math.floor((activeExpiresMs - now) / 1000))
  const activeMinutes = Math.floor(activeSecondsLeft / 60)
  const activeSeconds = activeSecondsLeft % 60
  const activeTimeDisplay = `${String(activeMinutes).padStart(2, '0')}:${String(activeSeconds).padStart(2, '0')}`

  const columns = [
    {
      title: 'Mã HĐ / Ref',
      key: 'invoiceCode',
      width: 170,
      render: (_, record) => (
        <Space direction="vertical" size={2}>
          <span className="invoice-pill">{record.invoiceCode}</span>
          {record.transactionRef && (
            <Tag color="blue" style={{ fontSize: '11px', fontFamily: 'monospace' }}>
              Ref: {record.transactionRef}
            </Tag>
          )}
          <Text type="secondary" style={{ fontSize: '12px' }}>
            {record.paymentTime ? dayjs(record.paymentTime).format('DD/MM/YYYY HH:mm') : '---'}
          </Text>
        </Space>
      ),
    },
    {
      title: 'Khách hàng',
      key: 'customer',
      width: 180,
      render: (_, record) => (
        <Space direction="vertical" size={1}>
          <Text strong style={{ color: '#0f172a' }}>
            {record.customerName || 'Khách vãng lai'}
          </Text>
          {record.customerPhone ? (
            <Text type="secondary" style={{ fontSize: '12px' }}>
              {record.customerPhone}
            </Text>
          ) : (
            <Text type="secondary" style={{ fontSize: '11px', fontStyle: 'italic' }}>
              Mua tại quầy
            </Text>
          )}
        </Space>
      ),
    },
    {
      title: 'Loại vé / Chi tiết',
      dataIndex: 'itemsSummary',
      key: 'itemsSummary',
      width: 240,
      render: (itemsSummary) => (
        <Tooltip title={itemsSummary}>
          <Text ellipsis style={{ maxWidth: 230, color: '#334155', fontWeight: 500 }}>
            {itemsSummary || 'Vé bơi tiêu chuẩn'}
          </Text>
        </Tooltip>
      ),
    },
    {
      title: 'Phương thức',
      dataIndex: 'paymentMethod',
      key: 'paymentMethod',
      width: 140,
      render: () => (
        <span className="method-pill method-transfer">
          <CreditCardOutlined />
          <span>Chuyển khoản QR</span>
        </span>
      ),
    },
    {
      title: 'Số tiền',
      dataIndex: 'amount',
      key: 'amount',
      width: 130,
      render: (amount) => (
        <Text strong style={{ color: '#0077b6', fontSize: '15px' }}>
          {formatCurrency(amount)}
        </Text>
      ),
    },
    {
      title: 'Thời gian còn lại',
      key: 'countdown',
      width: 150,
      render: (_, record) => {
        const createdMs = record.createdAt
          ? new Date(record.createdAt).getTime()
          : new Date(record.paymentTime).getTime()
        const expiresMs = record.expiresAtUtc
          ? new Date(record.expiresAtUtc).getTime()
          : createdMs + 5 * 60 * 1000
        const secondsLeft = Math.max(0, Math.floor((expiresMs - now) / 1000))
        const minutes = Math.floor(secondsLeft / 60)
        const seconds = secondsLeft % 60
        const isUrgent = secondsLeft < 120 && secondsLeft > 0
        const isExpired = secondsLeft <= 0

        if (isExpired) {
          return <Tag color="error">Đã hết hạn (5p)</Tag>
        }

        return (
          <Tag
            color={isUrgent ? 'warning' : 'processing'}
            icon={<ClockCircleOutlined />}
            style={{ fontWeight: 600, fontSize: '13px', padding: '4px 8px' }}
          >
            Còn {String(minutes).padStart(2, '0')}:{String(seconds).padStart(2, '0')}
          </Tag>
        )
      },
    },
    {
      title: 'Hành động',
      key: 'action',
      width: 170,
      align: 'center',
      render: (_, record) => (
        <Button
          type="primary"
          icon={<QrcodeOutlined />}
          size="middle"
          className="action-btn-pay"
          onClick={() => handleOpenPayment(record)}
        >
          Tiếp tục thanh toán
        </Button>
      ),
    },
  ]

  return (
    <div className="waiting-invoices-page">
      {/* 1. Ocean Hero Banner */}
      <section className="waiting-hero">
        <div className="waiting-hero-content">
          <span className="waiting-eyebrow">QUẢN LÝ GIAO DỊCH ĐANG CHỜ</span>
          <Title level={2} className="waiting-hero-title">
            Hàng chờ thanh toán
          </Title>
        </div>
        <div className="waiting-hero-icon">
          <ClockCircleOutlined />
        </div>
      </section>

      {/* 2. Filter & Action Card */}
      <Card className="waiting-filter-card" size="small">
        <Row gutter={[12, 12]} align="middle" justify="space-between">
          <Col xs={24} sm={16} md={12}>
            <Input
              placeholder="Tìm theo tên khách, SĐT, mã ref đơn hàng..."
              prefix={<SearchOutlined style={{ color: '#94a3b8' }} />}
              value={search}
              allowClear
              onChange={(e) => {
                setSearch(e.target.value)
                setPageIndex(1)
              }}
            />
          </Col>
          <Col xs={24} sm={8} md={6} style={{ textAlign: 'right' }}>
            <Button
              icon={<ReloadOutlined />}
              onClick={() => refetch()}
              loading={isLoading}
            >
              Làm mới danh sách
            </Button>
          </Col>
        </Row>
      </Card>

      {/* 4. Table */}
      <Card className="waiting-table-card">
        <Table
          rowKey="paymentId"
          columns={columns}
          dataSource={data?.items || []}
          loading={isLoading}
          pagination={{
            current: pageIndex,
            pageSize,
            total: data?.totalCount || 0,
            showSizeChanger: true,
            pageSizeOptions: ['10', '20', '50'],
            onChange: (p, s) => {
              setPageIndex(p)
              setPageSize(s)
            },
            showTotal: (total) => `Tổng số ${total} giao dịch đang chờ`,
          }}
          locale={{ emptyText: 'Không có giao dịch nào đang chờ thanh toán.' }}
        />
      </Card>

      {/* 5. Modal Tiếp tục thanh toán */}
      <Modal
        title={
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', paddingRight: 24 }}>
            <span style={{ fontSize: 18, fontWeight: 700, color: '#0077b6' }}>
              Tiếp tục thanh toán: {activeRecord?.invoiceCode}
            </span>
            {activeSecondsLeft > 0 ? (
              <Tag color={activeSecondsLeft < 120 ? 'error' : 'warning'} icon={<ClockCircleOutlined />}>
                Còn {activeTimeDisplay}
              </Tag>
            ) : (
              <Tag color="error">Đã hết hạn</Tag>
            )}
          </div>
        }
        open={qrModalOpen}
        onCancel={handleCloseModal}
        footer={null}
        centered
        width={420}
        maskClosable={false}
      >
        {paymentSuccessData ? (
          <div style={{ textAlign: 'center', padding: '24px 8px' }}>
            <CheckCircleOutlined style={{ fontSize: 56, color: '#10b981', marginBottom: 16 }} />
            <Title level={4} style={{ color: '#10b981', margin: '0 0 8px' }}>
              Thanh toán thành công!
            </Title>
            <p style={{ color: '#64748b', fontSize: 14, marginBottom: 20 }}>
              Giao dịch đã được hệ thống ghi nhận và chuyển vào Lịch sử thanh toán.
            </p>
            <Space size={12}>
              <Button
                type="primary"
                icon={<PrinterOutlined />}
                onClick={() => {
                  window.open(`/admin/payments/invoices/${activeRecord?.paymentId}/print`, '_blank')
                }}
              >
                In hóa đơn điện tử
              </Button>
              <Button onClick={handleCloseModal}>Đóng</Button>
            </Space>
          </div>
        ) : (
          <div style={{ textAlign: 'center' }}>
            <p style={{ fontSize: 14, color: '#475569', marginBottom: 12 }}>
              Vui lòng yêu cầu khách hàng quét mã VietQR bên dưới:
            </p>

            {/* Cảnh báo chuyển khoản thiếu */}
            {orderStatusData?.status === 'PARTIAL' && (
              <Alert
                type="warning"
                showIcon
                style={{ textAlign: 'left', marginBottom: 14 }}
                message="Khách chuyển thiếu tiền!"
                description={
                  <div>
                    <div>Đã nhận: <strong>{formatCurrency(orderStatusData.paidAmount)}</strong></div>
                    <div>Còn thiếu: <strong style={{ color: '#e11d48' }}>{formatCurrency(orderStatusData.remainingAmount)}</strong></div>
                    <div style={{ fontSize: 12, marginTop: 4 }}>
                      Mã QR bên dưới đã cập nhật đúng số tiền còn lại để khách quét nốt.
                    </div>
                  </div>
                }
              />
            )}

            <div
              style={{
                background: '#f8fafc',
                padding: 16,
                borderRadius: 12,
                border: '1px solid #e2e8f0',
                display: 'inline-block',
                marginBottom: 16,
              }}
            >
              {activeVietQRUrl ? (
                <img
                  src={activeVietQRUrl}
                  alt="VietQR"
                  style={{ width: '100%', maxWidth: 280, borderRadius: 8 }}
                />
              ) : (
                <Spin size="large" />
              )}
            </div>

            <div style={{ marginBottom: 12, background: '#f1f5f9', padding: '10px 16px', borderRadius: 8 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                <span style={{ color: '#64748b' }}>Khách hàng:</span>
                <strong>{activeRecord?.customerName || 'Khách vãng lai'}</strong>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                <span style={{ color: '#64748b' }}>Chi tiết:</span>
                <span style={{ fontWeight: 500 }}>{activeRecord?.itemsSummary}</span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                <span style={{ color: '#64748b' }}>Mã chuyển khoản:</span>
                <strong style={{ color: '#0284c7' }}>{activeTxRef}</strong>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', borderTop: '1px solid #cbd5e1', paddingTop: 6, marginTop: 6 }}>
                <span style={{ color: '#0f172a', fontWeight: 600 }}>Cần thanh toán:</span>
                <strong style={{ color: '#0284c7', fontSize: 17 }}>{formatCurrency(qrAmount)}</strong>
              </div>
            </div>

            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#0284c7',
                fontSize: 15,
                fontWeight: 600,
              }}
            >
              <LoadingOutlined style={{ marginRight: 8 }} /> Đang chờ hệ thống xác nhận tiền về...
            </div>

            <div style={{ marginTop: 12, fontSize: 12, color: '#94a3b8' }}>
              ⏱️ Đơn hàng sẽ tự động hủy khi hết thời gian 5 phút.
            </div>

            <div style={{ marginTop: 16, borderTop: '1px solid #e2e8f0', paddingTop: 12, display: 'flex', justifyContent: 'flex-end' }}>
              <Button onClick={handleCloseModal}>Đóng</Button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  )
}

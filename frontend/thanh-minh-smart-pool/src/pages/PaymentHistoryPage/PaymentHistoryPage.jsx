import { useState, useMemo } from 'react'
import {
  Table,
  Button,
  Space,
  Typography,
  Card,
  Badge,
  Input,
  Select,
  DatePicker,
  Row,
  Col,
  Tooltip,
  Statistic
} from 'antd'
import {
  EyeOutlined,
  PrinterOutlined,
  CreditCardOutlined,
  MoneyCollectOutlined,
  SearchOutlined,
  ReloadOutlined,
  FilterOutlined,
  TransactionOutlined,
  CheckCircleOutlined,
  ClockCircleOutlined,
  WalletOutlined,
  AuditOutlined
} from '@ant-design/icons'
import dayjs from 'dayjs'
import { usePayments } from '../../features/payments/hooks/usePayments'
import PaymentDetailModal from '../../features/payments/components/PaymentDetailModal'
import './PaymentHistoryPage.css'

const { Title, Text } = Typography
const { RangePicker } = DatePicker

const formatCurrency = (val) => {
  if (val === null || val === undefined) return '0 ₫'
  return new Intl.NumberFormat('vi-VN').format(val) + ' ₫'
}

export default function PaymentHistoryPage() {
  // Query states
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [status, setStatus] = useState(undefined)
  const [paymentMethod, setPaymentMethod] = useState(undefined)
  const [search, setSearch] = useState('')
  const [dateRange, setDateRange] = useState(null)

  // Modal detail state
  const [selectedPaymentId, setSelectedPaymentId] = useState(null)
  const [detailModalOpen, setDetailModalOpen] = useState(false)

  // Format dates for backend query
  const queryParams = {
    pageIndex,
    pageSize,
    status: status || undefined,
    paymentMethod: paymentMethod || undefined,
    search: search.trim() || undefined,
    fromDate: dateRange && dateRange[0] ? dateRange[0].startOf('day').toISOString() : undefined,
    toDate: dateRange && dateRange[1] ? dateRange[1].endOf('day').toISOString() : undefined,
  }

  // Call API from backend
  const { data, isLoading, refetch } = usePayments(queryParams)

  // Calculate quick stats from current data
  const stats = useMemo(() => {
    const items = data?.items || []
    const totalCount = data?.totalCount || 0
    const completed = items.filter((x) => x.status === 'COMPLETED')
    const completedRevenue = completed.reduce((sum, item) => sum + (item.amount || 0), 0)
    const pendingOrFailed = items.filter((x) => x.status !== 'COMPLETED').length

    return {
      totalCount,
      completedCount: completed.length,
      completedRevenue,
      pendingOrFailed,
    }
  }, [data])

  const handleOpenDetail = (paymentId) => {
    setSelectedPaymentId(paymentId)
    setDetailModalOpen(true)
  }

  const handleCloseDetail = () => {
    setDetailModalOpen(false)
    setSelectedPaymentId(null)
  }

  const handleResetFilters = () => {
    setSearch('')
    setStatus(undefined)
    setPaymentMethod(undefined)
    setDateRange(null)
    setPageIndex(1)
  }

  const columns = [
    {
      title: 'Mã HĐ & Ngày giờ',
      key: 'invoiceCode',
      width: 170,
      render: (_, record) => (
        <Space direction="vertical" size={2}>
          <span className="invoice-pill">{record.invoiceCode}</span>
          <Text orientation="left" type="secondary" style={{ fontSize: '12px' }}>
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
      title: 'Loại vé / Dịch vụ',
      dataIndex: 'itemsSummary',
      key: 'itemsSummary',
      width: 230,
      render: (itemsSummary) => (
        <Tooltip title={itemsSummary}>
          <Text ellipsis style={{ maxWidth: 220, color: '#334155', fontWeight: 500 }}>
            {itemsSummary || 'Vé bơi tiêu chuẩn'}
          </Text>
        </Tooltip>
      ),
    },
    {
      title: 'Phương thức',
      dataIndex: 'paymentMethod',
      key: 'paymentMethod',
      width: 150,
      render: (method) => {
        const isTransfer = method === 'BANK_TRANSFER'
        return isTransfer ? (
          <span className="method-pill method-transfer">
            <CreditCardOutlined />
            <span>Chuyển khoản</span>
          </span>
        ) : (
          <span className="method-pill method-cash">
            <MoneyCollectOutlined />
            <span>Tiền mặt</span>
          </span>
        )
      },
    },
    {
      title: 'Số tiền',
      dataIndex: 'amount',
      key: 'amount',
      width: 140,
      render: (amount) => (
        <Text strong style={{ color: '#0077b6', fontSize: '15px' }}>
          {formatCurrency(amount)}
        </Text>
      ),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      key: 'status',
      width: 140,
      render: (st) => {
        const isSuccess = st === 'COMPLETED'
        const isFailed = st === 'FAILED'
        const label = isSuccess ? 'Thành công' : isFailed ? 'Thất bại' : 'Chờ xử lý'
        const statusType = isSuccess ? 'success' : isFailed ? 'error' : 'warning'
        const badgeClass = isSuccess ? 'status-success' : isFailed ? 'status-failed' : 'status-warning'

        return (
          <Badge
            status={statusType}
            text={
              <Text
                style={{
                  color: isSuccess ? '#059669' : isFailed ? '#dc2626' : '#d97706',
                  fontWeight: 600,
                  whiteSpace: 'nowrap',
                }}
              >
                {label}
              </Text>
            }
            className={`status-badge ${badgeClass}`}
          />
        )
      },
    },
    {
      title: 'Hành động',
      key: 'action',
      width: 110,
      align: 'center',
      render: (_, record) => (
        <Space size={8}>
          <Tooltip title="Xem chi tiết giao dịch">
            <Button
              shape="circle"
              icon={<EyeOutlined />}
              className="action-btn-view"
              onClick={() => handleOpenDetail(record.paymentId)}
            />
          </Tooltip>
          <Tooltip title="Xuất hóa đơn điện tử">
            <Button
              shape="circle"
              icon={<PrinterOutlined />}
              className="action-btn-print"
              onClick={() => window.open(`/admin/payments/invoices/${record.paymentId}/print`, '_blank')}
            />
          </Tooltip>
        </Space>
      ),
    },
  ]

  return (
    <div className="payment-history-page">
      {/* 1. Ocean Hero Banner */}
      <section className="payment-hero">
        <div className="payment-hero-content">
          <span className="payment-eyebrow">QUẢN LÝ TÀI CHÍNH & DOANH THU</span>
          <Title level={2} className="payment-hero-title">
            Lịch sử giao dịch & Thanh toán
          </Title>
          <p>
            Theo dõi dòng tiền, quản lý biên lai và đối soát toàn bộ giao dịch mua vé, thuê đồ tại bể bơi
          </p>
        </div>
        <div className="payment-hero-icon">
          <TransactionOutlined />
        </div>
      </section>

      {/* 2. Quick KPI Statistics */}
      <Row gutter={[16, 16]} className="payment-stats">
        <Col xs={12} sm={6}>
          <Card className="payment-stat-card payment-stat-revenue">
            <Statistic
              title="Doanh thu trang này"
              value={formatCurrency(stats.completedRevenue)}
              prefix={<WalletOutlined style={{ color: '#0077b6' }} />}
              valueStyle={{ color: '#0077b6' }}
            />
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card className="payment-stat-card payment-stat-total">
            <Statistic
              title="Tổng giao dịch"
              value={stats.totalCount}
              prefix={<TransactionOutlined style={{ color: '#00b4d8' }} />}
              valueStyle={{ color: '#0f172a' }}
            />
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card className="payment-stat-card payment-stat-success">
            <Statistic
              title="Thành công (trang này)"
              value={stats.completedCount}
              prefix={<CheckCircleOutlined style={{ color: '#10b981' }} />}
              valueStyle={{ color: '#10b981' }}
            />
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card className="payment-stat-card payment-stat-pending">
            <Statistic
              title="Chờ xử lý / Khác"
              value={stats.pendingOrFailed}
              prefix={<ClockCircleOutlined style={{ color: '#f59e0b' }} />}
              valueStyle={{ color: '#f59e0b' }}
            />
          </Card>
        </Col>
      </Row>

      {/* 3. Filter Card */}
      <Card className="payment-filter-card" size="small">
        <div className="payment-filter-title">
          <FilterOutlined style={{ color: '#0077b6' }} />
          <span>Bộ lọc & Tìm kiếm giao dịch</span>
        </div>
        <Row gutter={[12, 12]} align="middle">
          <Col xs={24} sm={12} md={7}>
            <Input
              placeholder="Tìm theo tên khách, SĐT, mã ref..."
              prefix={<SearchOutlined style={{ color: '#94a3b8' }} />}
              value={search}
              allowClear
              onChange={(e) => {
                setSearch(e.target.value)
                setPageIndex(1)
              }}
            />
          </Col>

          <Col xs={12} sm={6} md={4}>
            <Select
              style={{ width: '100%' }}
              placeholder="Trạng thái"
              value={status}
              allowClear
              onChange={(val) => {
                setStatus(val)
                setPageIndex(1)
              }}
              options={[
                { value: 'COMPLETED', label: 'Thành công' },
                { value: 'FAILED', label: 'Thất bại' },
                { value: 'PENDING', label: 'Chờ xử lý' },
              ]}
            />
          </Col>

          <Col xs={12} sm={6} md={4}>
            <Select
              style={{ width: '100%' }}
              placeholder="Phương thức"
              value={paymentMethod}
              allowClear
              onChange={(val) => {
                setPaymentMethod(val)
                setPageIndex(1)
              }}
              options={[
                { value: 'CASH', label: 'Tiền mặt' },
                { value: 'BANK_TRANSFER', label: 'Chuyển khoản' },
              ]}
            />
          </Col>

          <Col xs={24} sm={12} md={6}>
            <RangePicker
              style={{ width: '100%' }}
              placeholder={['Từ ngày', 'Đến ngày']}
              format="DD/MM/YYYY"
              value={dateRange}
              onChange={(val) => {
                setDateRange(val)
                setPageIndex(1)
              }}
            />
          </Col>

          <Col xs={24} sm={12} md={3} style={{ textAlign: 'right' }}>
            <Space>
              <Tooltip title="Làm mới dữ liệu">
                <Button
                  icon={<ReloadOutlined />}
                  onClick={() => refetch()}
                  loading={isLoading}
                  style={{ borderColor: '#00b4d8', color: '#0077b6' }}
                />
              </Tooltip>
              <Tooltip title="Đặt lại bộ lọc">
                <Button icon={<FilterOutlined />} onClick={handleResetFilters}>
                  Đặt lại
                </Button>
              </Tooltip>
            </Space>
          </Col>
        </Row>
      </Card>

      {/* 4. Table Card */}
      <Card bordered={false} className="payment-table-card" bodyStyle={{ padding: 0 }}>
        <Table
          columns={columns}
          dataSource={data?.items || []}
          rowKey="paymentId"
          loading={isLoading}
          scroll={{ x: 1000 }}
          pagination={{
            current: pageIndex,
            pageSize: pageSize,
            total: data?.totalCount || 0,
            showSizeChanger: true,
            pageSizeOptions: ['5', '10', '20', '50'],
            position: ['bottomCenter'],
            onChange: (p, ps) => {
              setPageIndex(p)
              setPageSize(ps)
            },
            showTotal: (total) => `Tổng cộng ${total} giao dịch`,
          }}
          rowClassName="payment-table-row"
          locale={{
            emptyText: (
              <div style={{ padding: '30px 0', color: '#64748b' }}>
                <AuditOutlined style={{ fontSize: '36px', color: '#cbd5e1', marginBottom: 10 }} />
                <div>Chưa có giao dịch nào phù hợp với bộ lọc</div>
              </div>
            ),
          }}
        />
      </Card>

      {/* 5. Modal Chi Tiết Giao Dịch */}
      {detailModalOpen && (
        <PaymentDetailModal
          paymentId={selectedPaymentId}
          open={detailModalOpen}
          onCancel={handleCloseDetail}
        />
      )}
    </div>
  )
}

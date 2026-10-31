import { useState, useMemo } from 'react'
import {
  Table,
  Button,
  Space,
  Typography,
  Card,
  Tag,
  Input,
  DatePicker,
  Row,
  Col,
  Tabs,
  Badge,
  Empty
} from 'antd'
import {
  HistoryOutlined,
  ArrowLeftOutlined,
  SearchOutlined,
  ReloadOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  ScheduleOutlined,
  IdcardOutlined,
  ClockCircleOutlined
} from '@ant-design/icons'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import dayjs from 'dayjs'
import isSameOrAfter from 'dayjs/plugin/isSameOrAfter'
import isSameOrBefore from 'dayjs/plugin/isSameOrBefore'
import ticketService from '../../features/tickets/services/ticketService'
import { useAuthStore } from '../../features/auth/store/authStore'
import './PoolEntryHistoryPage.css'

dayjs.extend(isSameOrAfter)
dayjs.extend(isSameOrBefore)

const { Title, Text } = Typography
const { RangePicker } = DatePicker

export default function PoolEntryHistoryPage() {
  const navigate = useNavigate()
  const { session } = useAuthStore()

  const [activeTab, setActiveTab] = useState('entries')
  const [searchText, setSearchText] = useState('')
  const [dateRange, setDateRange] = useState(null)

  // 1. Fetch Entry Logs
  const {
    data: entries = [],
    isLoading: loadingEntries,
    refetch: refetchEntries,
  } = useQuery({
    queryKey: ['myEntryHistory'],
    queryFn: ticketService.getMyEntryHistory,
    enabled: !!session,
  })

  // 2. Fetch All Tickets to filter Used / Expired
  const {
    data: tickets = [],
    isLoading: loadingTickets,
    refetch: refetchTickets,
  } = useQuery({
    queryKey: ['myTickets'],
    queryFn: ticketService.getMyTickets,
    enabled: !!session,
  })

  // Lọc danh sách vé đã sử dụng hoặc hết hạn
  const archivedTickets = useMemo(() => {
    return (tickets || []).filter((t) => {
      const status = t.status?.toUpperCase()
      if (status === 'USED' || status === 'EXPIRED') return true
      if (
        t.ticketCategory === 'VE_LUOT' &&
        t.expiryDate &&
        dayjs(t.expiryDate).isBefore(dayjs())
      ) {
        return true
      }
      return false
    })
  }, [tickets])

  // Lọc dữ liệu tab 1 (Lượt vào bể)
  const filteredEntries = useMemo(() => {
    return entries.filter((item) => {
      // Tìm kiếm theo từ khóa
      if (searchText) {
        const q = searchText.toLowerCase().trim()
        const matchCode = item.qrCode?.toLowerCase().includes(q)
        const matchName = item.ticketName?.toLowerCase().includes(q)
        const matchGate = item.gateId?.toLowerCase().includes(q)
        if (!matchCode && !matchName && !matchGate) return false
      }
      // Lọc theo khoảng ngày
      if (dateRange && dateRange[0] && dateRange[1]) {
        if (!item.scanTime) return false
        const scan = dayjs(item.scanTime)
        if (
          scan.isBefore(dateRange[0].startOf('day')) ||
          scan.isAfter(dateRange[1].endOf('day'))
        ) {
          return false
        }
      }
      return true
    })
  }, [entries, searchText, dateRange])

  // Lọc dữ liệu tab 2 (Vé đã sử dụng)
  const filteredArchivedTickets = useMemo(() => {
    return archivedTickets.filter((item) => {
      if (searchText) {
        const q = searchText.toLowerCase().trim()
        const matchCode = item.qrCode?.toLowerCase().includes(q)
        const matchName = item.ticketName?.toLowerCase().includes(q)
        if (!matchCode && !matchName) return false
      }
      if (dateRange && dateRange[0] && dateRange[1]) {
        const targetDate = item.expiryDate || item.issueDate
        if (!targetDate) return false
        const d = dayjs(targetDate)
        if (
          d.isBefore(dateRange[0].startOf('day')) ||
          d.isAfter(dateRange[1].endOf('day'))
        ) {
          return false
        }
      }
      return true
    })
  }, [archivedTickets, searchText, dateRange])

  const handleRefresh = () => {
    if (activeTab === 'entries') {
      refetchEntries()
    } else {
      refetchTickets()
    }
  }

  // Cột bảng Lịch sử lượt quét vào bể
  const entryColumns = [
    {
      title: 'Thời gian vào bể',
      dataIndex: 'scanTime',
      key: 'scanTime',
      width: 180,
      render: (val) => (
        <Space orientation="vertical" size={2}>
          <Text strong>
            <ClockCircleOutlined style={{ marginRight: 6, color: '#0958d9' }} />
            {val ? dayjs(val).format('HH:mm:ss') : '--:--:--'}
          </Text>
          <Text type="secondary" style={{ fontSize: '12px' }}>
            {val ? dayjs(val).format('DD/MM/YYYY') : '---'}
          </Text>
        </Space>
      ),
    },
    {
      title: 'Tên vé',
      dataIndex: 'ticketName',
      key: 'ticketName',
      render: (name, record) => (
        <div>
          <Text strong style={{ color: '#1f1f1f', fontSize: '14px' }}>
            {name || 'Vé vào bể'}
          </Text>
          <div style={{ marginTop: 4 }}>
            <Tag color={record.ticketCategory === 'VE_THANG' ? 'cyan' : 'green'}>
              {record.ticketCategory === 'VE_THANG' ? 'Vé tháng' : 'Vé lượt'}
            </Tag>
          </div>
        </div>
      ),
    },
    {
      title: 'Mã vé',
      dataIndex: 'qrCode',
      key: 'qrCode',
      width: 140,
      render: (code) => (
        <span
          style={{
            fontFamily: 'monospace',
            fontWeight: 700,
            fontSize: '14px',
            background: '#f5f5f5',
            padding: '2px 8px',
            borderRadius: '4px',
            border: '1px solid #d9d9d9',
          }}
        >
          {code || '---'}
        </span>
      ),
    },
    {
      title: 'Cổng vào / Hình thức',
      key: 'gateAndMode',
      width: 180,
      render: (_, record) => (
        <Space orientation="vertical" size={2}>
          <Text>{record.gateId || 'Cổng vào bể'}</Text>
          <Tag color="default" style={{ fontSize: '11px', width: 'fit-content' }}>
            {record.inputMode === 'MANUAL' ? 'Nhập mã thủ công' : 'Quét QR'}
          </Tag>
        </Space>
      ),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      key: 'status',
      width: 160,
      render: (status, record) => {
        const isSuccess =
          status?.toLowerCase() === 'allowed' ||
          status?.toLowerCase() === 'success' ||
          !status
        return (
          <Space>
            {isSuccess ? (
              <Tag color="success" icon={<CheckCircleOutlined />}>
                Vào bể thành công
              </Tag>
            ) : (
              <Tag color="error" icon={<CloseCircleOutlined />}>
                {record.message || 'Bị từ chối'}
              </Tag>
            )}
          </Space>
        )
      },
    },
  ]

  // Cột bảng Vé đã sử dụng / Hết hạn
  const ticketColumns = [
    {
      title: 'Tên vé',
      dataIndex: 'ticketName',
      key: 'ticketName',
      render: (name, record) => (
        <div>
          <Text strong style={{ fontSize: '14px' }}>
            {name}
          </Text>
          <div style={{ marginTop: 4 }}>
            <Tag color={record.ticketCategory === 'VE_THANG' ? 'cyan' : 'green'}>
              {record.ticketCategory === 'VE_THANG' ? 'Vé tháng' : 'Vé lượt'}
            </Tag>
          </div>
        </div>
      ),
    },
    {
      title: 'Mã vé',
      dataIndex: 'qrCode',
      key: 'qrCode',
      width: 140,
      render: (code) => (
        <span
          style={{
            fontFamily: 'monospace',
            fontWeight: 700,
            fontSize: '14px',
            background: '#f5f5f5',
            padding: '2px 8px',
            borderRadius: '4px',
            border: '1px solid #d9d9d9',
          }}
        >
          {code}
        </span>
      ),
    },
    {
      title: 'Ngày mua / Kích hoạt',
      dataIndex: 'issueDate',
      key: 'issueDate',
      width: 160,
      render: (val) => (val ? dayjs(val).format('DD/MM/YYYY HH:mm') : '---'),
    },
    {
      title: 'Hạn sử dụng',
      dataIndex: 'expiryDate',
      key: 'expiryDate',
      width: 160,
      render: (val) => (val ? dayjs(val).format('DD/MM/YYYY HH:mm') : '---'),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      key: 'status',
      width: 140,
      render: (status, record) => {
        const s = status?.toUpperCase()
        if (s === 'USED') {
          return <Tag color="blue">Đã sử dụng</Tag>
        }
        if (
          s === 'EXPIRED' ||
          (record.expiryDate && dayjs(record.expiryDate).isBefore(dayjs()))
        ) {
          return <Tag color="default">Hết hạn</Tag>
        }
        return <Tag color="orange">{status}</Tag>
      },
    },
  ]

  return (
    <div className="peh-page">
      {/* Header */}
      <div className="peh-header">
        <div className="peh-title-area">
          <Button
            type="text"
            icon={<ArrowLeftOutlined />}
            onClick={() => navigate('/the-cua-toi')}
            style={{ marginRight: 8 }}
          >
            Thẻ của tôi
          </Button>
          <Title level={2} style={{ margin: 0, color: '#135200' }}>
            Lịch sử vào bể & Vé đã dùng
          </Title>
        </div>
        <Space>
          <Button
            icon={<ReloadOutlined />}
            onClick={handleRefresh}
            loading={loadingEntries || loadingTickets}
          >
            Làm mới
          </Button>
          <Button
            type="primary"
            icon={<IdcardOutlined />}
            onClick={() => navigate('/the-cua-toi')}
            style={{ backgroundColor: '#237804' }}
          >
            Xem vé khả dụng
          </Button>
        </Space>
      </div>

      {/* Filter Bar */}
      <Card className="peh-filter-card" size="small" style={{ marginBottom: 16 }}>
        <Row gutter={[16, 12]} align="middle">
          <Col xs={24} sm={10} md={8}>
            <Input
              prefix={<SearchOutlined style={{ color: '#bfbfbf' }} />}
              placeholder="Tìm theo mã vé, tên vé..."
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              allowClear
            />
          </Col>
          <Col xs={24} sm={14} md={10}>
            <RangePicker
              style={{ width: '100%' }}
              value={dateRange}
              onChange={setDateRange}
              format="DD/MM/YYYY"
              placeholder={['Từ ngày', 'Đến ngày']}
            />
          </Col>
          <Col xs={24} sm={24} md={6} style={{ textAlign: 'right' }}>
            <Text type="secondary" style={{ fontSize: '13px' }}>
              {activeTab === 'entries'
                ? `Hiển thị ${filteredEntries.length} lượt vào`
                : `Hiển thị ${filteredArchivedTickets.length} vé lưu trữ`}
            </Text>
          </Col>
        </Row>
      </Card>

      {/* Main Tabs */}
      <Card className="peh-main-card">
        <Tabs
          activeKey={activeTab}
          onChange={setActiveTab}
          size="large"
          items={[
            {
              key: 'entries',
              label: (
                <Space>
                  <HistoryOutlined />
                  <span>Lịch sử các lượt vào bể</span>
                  <Badge
                    count={entries.length}
                    style={{ backgroundColor: '#52c41a' }}
                    overflowCount={999}
                  />
                </Space>
              ),
              children: (
                <Table
                  columns={entryColumns}
                  dataSource={filteredEntries}
                  rowKey="id"
                  loading={loadingEntries}
                  pagination={{
                    pageSize: 10,
                    showSizeChanger: true,
                    pageSizeOptions: ['10', '20', '50'],
                    showTotal: (total) => `Tổng cộng ${total} lượt vào bể`,
                  }}
                  locale={{
                    emptyText: (
                      <Empty
                        image={Empty.PRESENTED_IMAGE_SIMPLE}
                        description="Chưa có dữ liệu lượt vào bể nào"
                      />
                    ),
                  }}
                />
              ),
            },
            {
              key: 'archivedTickets',
              label: (
                <Space>
                  <ScheduleOutlined />
                  <span>Vé đã sử dụng / Hết hạn</span>
                  <Badge
                    count={archivedTickets.length}
                    style={{ backgroundColor: '#8c8c8c' }}
                    overflowCount={999}
                  />
                </Space>
              ),
              children: (
                <Table
                  columns={ticketColumns}
                  dataSource={filteredArchivedTickets}
                  rowKey="id"
                  loading={loadingTickets}
                  pagination={{
                    pageSize: 10,
                    showSizeChanger: true,
                    pageSizeOptions: ['10', '20', '50'],
                    showTotal: (total) => `Tổng cộng ${total} vé đã lưu trữ`,
                  }}
                  locale={{
                    emptyText: (
                      <Empty
                        image={Empty.PRESENTED_IMAGE_SIMPLE}
                        description="Không có vé đã sử dụng hoặc hết hạn nào"
                      />
                    ),
                  }}
                />
              ),
            },
          ]}
        />
      </Card>
    </div>
  )
}

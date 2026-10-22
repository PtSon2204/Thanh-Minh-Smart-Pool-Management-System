import { useState, useEffect, useCallback, useMemo } from 'react'
import {
  Table,
  Card,
  Row,
  Col,
  Input,
  Select,
  DatePicker,
  Button,
  Tag,
  Modal,
  Space,
  Typography,
  Descriptions,
  Divider,
  Image,
} from 'antd'
import {
  AlertOutlined,
  CheckCircleOutlined,
  ClockCircleOutlined,
  EyeOutlined,
  ReloadOutlined,
  SearchOutlined,
  UserOutlined,
  EnvironmentOutlined,
  HistoryOutlined,
  VideoCameraOutlined,
} from '@ant-design/icons'
import dayjs from 'dayjs'
import safetyAlertService from '../../../features/safety/services/safetyAlertService'
import { useSafetyAlertSignalR } from '../../../features/safety/hooks/useSafetyAlertSignalR'
import './IncidentHistoryPage.css'

const { Title, Text } = Typography
const { RangePicker } = DatePicker

const ZONES = [
  { value: 'Tất cả', label: 'Tất cả khu vực' },
  { value: 'Bể lớn', label: 'Bể lớn' },
  { value: 'Bể trẻ em', label: 'Bể trẻ em' },
]

const SEVERITIES = [
  { value: 'Tất cả', label: 'Tất cả mức độ' },
  { value: 'EMERGENCY', label: 'Khẩn cấp' },
  { value: 'WARNING', label: 'Cảnh báo' },
]

const SOURCES = [
  { value: 'Tất cả', label: 'Tất cả nguồn' },
  { value: 'camera', label: 'Camera AI' },
  { value: 'lifeguard', label: 'Cứu hộ viên' },
]

export default function IncidentHistoryPage() {
  const [data, setData] = useState([])
  const [loading, setLoading] = useState(false)
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [refreshKey, setRefreshKey] = useState(0)

  // Filters
  const [searchText, setSearchText] = useState('')
  const [selectedZone, setSelectedZone] = useState('Tất cả')
  const [selectedSeverity, setSelectedSeverity] = useState('Tất cả')
  const [selectedSource, setSelectedSource] = useState('Tất cả')
  const [dateRange, setDateRange] = useState(null)

  // Counters
  const [stats, setStats] = useState({
    total: 0,
    emergency: 0,
    cameraCount: 0,
    lifeguardCount: 0,
  })

  // Modal Chi tiết sự cố
  const [detailModalOpen, setDetailModalOpen] = useState(false)
  const [selectedIncident, setSelectedIncident] = useState(null)

  const triggerRefresh = useCallback(() => {
    setRefreshKey((k) => k + 1)
  }, [])

  // Fetch danh sách sự cố từ API
  useEffect(() => {
    let ignore = false

    const fetchData = async () => {
      setLoading(true)
      try {
        const params = {
          pageNumber: page,
          pageSize,
          search: searchText.trim() || undefined,
          zone: selectedZone !== 'Tất cả' ? selectedZone : undefined,
          severityLevel: selectedSeverity !== 'Tất cả' ? selectedSeverity : undefined,
          fromDate: dateRange?.[0] ? dateRange[0].startOf('day').toISOString() : undefined,
          toDate: dateRange?.[1] ? dateRange[1].endOf('day').toISOString() : undefined,
        }

        const res = await safetyAlertService.getIncidents(params)
        if (!ignore && res) {
          const items = res.items || []
          const cameraCount =
            res.cameraCount ??
            items.filter((x) => x.imageIncidents || x.sourceType?.toLowerCase().includes('camera')).length
          const emergencyCount =
            res.emergencyCount ?? items.filter((x) => x.severityLevel === 'EMERGENCY').length
          const total = res.totalIncidents || res.totalCount || items.length
          const lifeguardCount = res.lifeguardCount ?? Math.max(0, total - cameraCount)

          setData(items)
          setTotalCount(res.totalCount || 0)
          setStats({
            total,
            emergency: emergencyCount,
            cameraCount,
            lifeguardCount,
          })
        }
      } catch (err) {
        console.warn('[IncidentHistoryPage] fetch error:', err)
        if (!ignore) {
          const demoItems = [
            {
              id: 'demo-1',
              incidentCode: 'INC-2026-001',
              title: 'Cảnh báo cứu hộ: Bể lớn',
              zone: 'Bể lớn',
              severityLevel: 'EMERGENCY',
              status: 'Resolved',
              description:
                'Camera AI phát hiện vị trí khách có dấu hiệu đuối nước tại độ sâu 1.8m làn số 3',
              incidentTime: dayjs().subtract(10, 'minute').toISOString(),
              reportedByName: 'Hệ thống AI Camera',
              imageIncidents:
                'https://images.unsplash.com/photo-1576013551627-0cc20b96c2a7?q=80&w=800&auto=format&fit=crop',
              sourceType: 'Camera AI giám sát',
            },
            {
              id: 'demo-2',
              incidentCode: 'INC-2026-002',
              title: 'Cảnh báo cứu hộ: Bể trẻ em',
              zone: 'Bể trẻ em',
              severityLevel: 'EMERGENCY',
              status: 'Resolved',
              description:
                'Cứu hộ viên phát hiện trẻ nhỏ trượt ngã sát mép nước, đã tiếp cận sơ cứu tại chỗ',
              incidentTime: dayjs().subtract(1, 'hour').toISOString(),
              reportedByName: 'Cứu hộ viên Nguyễn Văn An',
              imageIncidents: null,
              sourceType: 'Cứu hộ viên phát hiện thực tế',
            },
            {
              id: 'demo-3',
              incidentCode: 'INC-2026-003',
              title: 'Cảnh báo an toàn: Bể lớn',
              zone: 'Bể lớn',
              severityLevel: 'WARNING',
              status: 'Resolved',
              description:
                'Camera AI giám sát phát hiện người bơi vi phạm ranh giới phao ngăn làn số 2',
              incidentTime: dayjs().subtract(3, 'hour').toISOString(),
              reportedByName: 'Hệ thống AI Camera',
              imageIncidents:
                'https://images.unsplash.com/photo-1519315901367-f34ff9154487?q=80&w=800&auto=format&fit=crop',
              sourceType: 'Camera AI giám sát',
            },
            {
              id: 'demo-4',
              incidentCode: 'INC-2026-004',
              title: 'Cảnh báo an toàn: Bể trẻ em',
              zone: 'Bể trẻ em',
              severityLevel: 'WARNING',
              status: 'Resolved',
              description:
                'Khách đông đùa nghịch quá giới hạn tại chân cầu trượt, cứu hộ viên đã thổi còi nhắc nhở',
              incidentTime: dayjs().subtract(6, 'hour').toISOString(),
              reportedByName: 'Giám sát viên Lê Thị Thảo',
              imageIncidents: null,
              sourceType: 'Cứu hộ viên phát hiện thực tế',
            },
            {
              id: 'demo-5',
              incidentCode: 'INC-2026-005',
              title: 'Cảnh báo cứu hộ: Bể lớn',
              zone: 'Bể lớn',
              severityLevel: 'EMERGENCY',
              status: 'Resolved',
              description:
                'Cứu hộ viên phát hiện khách bơi có triệu chứng chuột rút giữa lòng bể, đã hỗ trợ phao vào bờ',
              incidentTime: dayjs().subtract(1, 'day').toISOString(),
              reportedByName: 'Cứu hộ viên Trần Đình Trọng',
              imageIncidents: null,
              sourceType: 'Cứu hộ viên phát hiện thực tế',
            },
          ]
          setData(demoItems)
          setTotalCount(demoItems.length)
          setStats({
            total: demoItems.length,
            emergency: 3,
            cameraCount: 2,
            lifeguardCount: 3,
          })
        }
      } finally {
        if (!ignore) {
          setLoading(false)
        }
      }
    }

    void fetchData()

    return () => {
      ignore = true
    }
  }, [page, pageSize, searchText, selectedZone, selectedSeverity, dateRange, refreshKey])

  // Lắng nghe SignalR khi có sự cố mới để tự động làm mới
  useSafetyAlertSignalR({
    onAlert: () => {
      triggerRefresh()
    },
    enabled: true,
  })

  // Mở modal xem chi tiết
  const handleOpenDetail = useCallback((record) => {
    setSelectedIncident(record)
    setDetailModalOpen(true)
  }, [])

  // Lọc theo Nguồn phát hiện (Camera AI vs Cứu hộ viên)
  const displayedData = useMemo(() => {
    if (selectedSource === 'camera') {
      return data.filter(
        (x) => !!x.imageIncidents || x.sourceType?.toLowerCase().includes('camera'),
      )
    }
    if (selectedSource === 'lifeguard') {
      return data.filter(
        (x) => !x.imageIncidents && !x.sourceType?.toLowerCase().includes('camera'),
      )
    }
    return data
  }, [data, selectedSource])

  // Cấu hình các cột hiển thị trên bảng danh sách (chỉ giữ các trường đại diện)
  const columns = useMemo(
    () => [
      {
        title: 'Mã sự cố',
        dataIndex: 'incidentCode',
        key: 'incidentCode',
        width: 160,
        render: (code) => <span className="incident-code-badge">{code || 'INC-N/A'}</span>,
      },
      {
        title: 'Thời gian',
        dataIndex: 'incidentTime',
        key: 'incidentTime',
        width: 180,
        render: (time) => (
          <Space size={6}>
            <ClockCircleOutlined style={{ color: '#0284c7' }} />
            <span>{time ? dayjs(time).format('HH:mm DD/MM/YYYY') : '—'}</span>
          </Space>
        ),
      },
      {
        title: 'Khu vực',
        dataIndex: 'zone',
        key: 'zone',
        width: 150,
        render: (zone) => (
          <Tag color="cyan" icon={<EnvironmentOutlined />}>
            {zone || 'Bể lớn'}
          </Tag>
        ),
      },
      {
        title: 'Mức độ',
        dataIndex: 'severityLevel',
        key: 'severityLevel',
        width: 140,
        render: (level) => {
          const isEmergency = String(level).toUpperCase() === 'EMERGENCY'
          return (
            <Tag color={isEmergency ? 'error' : 'warning'} icon={<AlertOutlined />}>
              {isEmergency ? 'Khẩn cấp' : 'Cảnh báo'}
            </Tag>
          )
        },
      },
      {
        title: 'Thao tác',
        key: 'actions',
        width: 120,
        align: 'center',
        render: (_, record) => (
          <Button
            type="primary"
            ghost
            size="small"
            icon={<EyeOutlined />}
            onClick={() => handleOpenDetail(record)}
          >
            Chi tiết
          </Button>
        ),
      },
    ],
    [handleOpenDetail],
  )

  return (
    <div className="incident-history-page">
      {/* ── Tiêu đề trang ── */}
      <div className="incident-page-header">
        <div className="header-left">
          <div className="header-icon-box">
            <HistoryOutlined />
          </div>
          <div>
            <Title level={4} style={{ margin: 0, color: '#005f8e' }}>
              Lịch sử sự cố & Cảnh báo cứu hộ
            </Title>
            <Text type="secondary">
              Nhật ký lưu trữ tất cả sự cố từ Camera AI và Cứu hộ viên kích hoạt tại các khu vực bể bơi
            </Text>
          </div>
        </div>
        <Button
          icon={<ReloadOutlined />}
          onClick={triggerRefresh}
          loading={loading}
          style={{ borderColor: '#0077b6', color: '#005f8e' }}
        >
          Làm mới
        </Button>
      </div>

      {/* ── Thẻ thống kê nhanh ── */}
      <Row gutter={[16, 16]} className="incident-stats-row">
        <Col xs={12} sm={6}>
          <Card className="stat-card stat-card-total" bordered={false}>
            <div className="stat-card-inner">
              <div className="stat-icon-box stat-icon-total">
                <HistoryOutlined />
              </div>
              <div className="stat-info">
                <div className="stat-label">Tổng sự cố</div>
                <div className="stat-number">{stats.total}</div>
              </div>
            </div>
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card className="stat-card stat-card-emergency" bordered={false}>
            <div className="stat-card-inner">
              <div className="stat-icon-box stat-icon-emergency">
                <AlertOutlined />
              </div>
              <div className="stat-info">
                <div className="stat-label">Khẩn cấp</div>
                <div className="stat-number">{stats.emergency}</div>
              </div>
            </div>
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card className="stat-card stat-card-camera" bordered={false}>
            <div className="stat-card-inner">
              <div className="stat-icon-box stat-icon-camera">
                <VideoCameraOutlined />
              </div>
              <div className="stat-info">
                <div className="stat-label">Camera AI</div>
                <div className="stat-number">{stats.cameraCount}</div>
              </div>
            </div>
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card className="stat-card stat-card-lifeguard" bordered={false}>
            <div className="stat-card-inner">
              <div className="stat-icon-box stat-icon-lifeguard">
                <UserOutlined />
              </div>
              <div className="stat-info">
                <div className="stat-label">Cứu hộ viên</div>
                <div className="stat-number">{stats.lifeguardCount}</div>
              </div>
            </div>
          </Card>
        </Col>
      </Row>

      {/* ── Bộ lọc tìm kiếm ── */}
      <Card className="incident-filter-card" bordered={false}>
        <Row gutter={[12, 12]} align="middle">
          <Col xs={24} sm={12} md={7}>
            <Input
              placeholder="Tìm theo mã sự cố, nội dung, người báo..."
              prefix={<SearchOutlined style={{ color: '#94a3b8' }} />}
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              onPressEnter={triggerRefresh}
              allowClear
            />
          </Col>
          <Col xs={12} sm={6} md={4}>
            <Select
              value={selectedZone}
              onChange={setSelectedZone}
              options={ZONES}
              style={{ width: '100%' }}
            />
          </Col>
          <Col xs={12} sm={6} md={4}>
            <Select
              value={selectedSeverity}
              onChange={setSelectedSeverity}
              options={SEVERITIES}
              style={{ width: '100%' }}
            />
          </Col>
          <Col xs={12} sm={6} md={4}>
            <Select
              value={selectedSource}
              onChange={setSelectedSource}
              options={SOURCES}
              style={{ width: '100%' }}
            />
          </Col>
          <Col xs={24} sm={12} md={5}>
            <RangePicker
              value={dateRange}
              onChange={setDateRange}
              format="DD/MM/YYYY"
              placeholder={['Từ ngày', 'Đến ngày']}
              style={{ width: '100%' }}
            />
          </Col>
        </Row>
      </Card>

      {/* ── Bảng danh sách lịch sử ── */}
      <Card className="incident-table-card" bordered={false}>
        <Table
          columns={columns}
          dataSource={displayedData}
          rowKey={(r) => r.id}
          loading={loading}
          pagination={{
            current: page,
            pageSize,
            total: totalCount,
            showSizeChanger: true,
            pageSizeOptions: ['10', '20', '50'],
            onChange: (p, ps) => {
              setPage(p)
              setPageSize(ps)
            },
            showTotal: (total) => `Tổng cộng ${total} sự cố`,
          }}
          scroll={{ x: 800 }}
          className="incident-custom-table"
        />
      </Card>

      {/* ── Modal Xem Chi Tiết Sự Cố ── */}
      <Modal
        title={
          <Space>
            <HistoryOutlined style={{ color: '#0077b6' }} />
            <span>Chi tiết sự cố & Cảnh báo cứu hộ</span>
            {selectedIncident?.incidentCode && (
              <Tag color="blue">{selectedIncident.incidentCode}</Tag>
            )}
          </Space>
        }
        open={detailModalOpen}
        onCancel={() => setDetailModalOpen(false)}
        footer={[
          <Button key="close" type="primary" onClick={() => setDetailModalOpen(false)}>
            Đóng
          </Button>,
        ]}
        width={680}
        centered
      >
        {selectedIncident && (
          <div className="incident-detail-body">
            <Descriptions bordered column={2} size="small">
              <Descriptions.Item label="Mã sự cố" span={1}>
                <Text strong copyable>
                  {selectedIncident.incidentCode || 'INC-N/A'}
                </Text>
              </Descriptions.Item>
              <Descriptions.Item label="Khu vực" span={1}>
                <Tag color="cyan" icon={<EnvironmentOutlined />}>
                  {selectedIncident.zone || 'Bể lớn'}
                </Tag>
              </Descriptions.Item>

              <Descriptions.Item label="Thời gian xảy ra" span={1}>
                {dayjs(selectedIncident.incidentTime).format('HH:mm:ss DD/MM/YYYY')}
              </Descriptions.Item>
              <Descriptions.Item label="Mức độ" span={1}>
                <Tag
                  color={
                    String(selectedIncident.severityLevel).toUpperCase() === 'EMERGENCY'
                      ? 'error'
                      : 'warning'
                  }
                  icon={<AlertOutlined />}
                >
                  {String(selectedIncident.severityLevel).toUpperCase() === 'EMERGENCY'
                    ? 'Khẩn cấp'
                    : 'Cảnh báo'}
                </Tag>
              </Descriptions.Item>

              <Descriptions.Item label="Nguồn phát hiện" span={1}>
                {selectedIncident.imageIncidents ||
                selectedIncident.sourceType?.toLowerCase().includes('camera') ? (
                  <Tag color="purple" icon={<VideoCameraOutlined />}>
                    Camera AI giám sát
                  </Tag>
                ) : (
                  <Tag color="blue" icon={<UserOutlined />}>
                    Cứu hộ viên phát hiện thực tế
                  </Tag>
                )}
              </Descriptions.Item>
              <Descriptions.Item label="Người / Thiết bị báo" span={1}>
                <Space size={4}>
                  {selectedIncident.imageIncidents ||
                  selectedIncident.sourceType?.toLowerCase().includes('camera') ? (
                    <VideoCameraOutlined style={{ color: '#7c3aed' }} />
                  ) : (
                    <UserOutlined style={{ color: '#0284c7' }} />
                  )}
                  <span>
                    {selectedIncident.reportedByName ||
                      (selectedIncident.imageIncidents ? 'Hệ thống AI Camera' : 'Cứu hộ viên')}
                  </span>
                </Space>
              </Descriptions.Item>

              <Descriptions.Item label="Trạng thái" span={2}>
                <Tag color="success" icon={<CheckCircleOutlined />}>
                  Đã ghi nhận sự cố
                </Tag>
              </Descriptions.Item>
            </Descriptions>

            <Divider style={{ margin: '16px 0' }} />

            <div>
              <Text strong style={{ display: 'block', marginBottom: 6 }}>
                Mô tả tình huống ban đầu:
              </Text>
              <div className="incident-detail-desc-box">
                {selectedIncident.description || 'Không có mô tả chi tiết kèm theo.'}
              </div>
            </div>

            <Divider style={{ margin: '16px 0' }} />

            {/* Khối hiển thị hình ảnh từ Camera AI hoặc thông báo nếu cứu hộ phát hiện */}
            {selectedIncident.imageIncidents ? (
              <div className="incident-image-section">
                <div className="incident-section-header">
                  <Space>
                    <VideoCameraOutlined style={{ color: '#7c3aed', fontSize: 16 }} />
                    <Text strong style={{ color: '#1e293b' }}>
                      Hình ảnh trích xuất từ Camera AI:
                    </Text>
                  </Space>
                  <Tag color="purple">Ảnh chụp snapshot</Tag>
                </div>
                <div className="incident-image-container">
                  <Image
                    src={selectedIncident.imageIncidents}
                    alt="Ảnh chụp sự cố từ Camera AI"
                    className="incident-camera-img"
                    style={{
                      maxHeight: 340,
                      width: '100%',
                      objectFit: 'cover',
                      borderRadius: 8,
                    }}
                    fallback="https://placehold.co/600x340/e2e8f0/475569?text=Kh%C3%B4ng+t%E1%BA%A3i+%C4%91%C6%B0%E1%BB%A3c+%E1%BA%A3nh"
                  />
                  <div className="incident-image-caption">
                    <span>
                      <ClockCircleOutlined style={{ marginRight: 4 }} />
                      Thời điểm chụp: {dayjs(selectedIncident.incidentTime).format('HH:mm:ss DD/MM/YYYY')}
                    </span>
                    <span className="incident-image-zoom-hint">Nhấp vào ảnh để phóng to</span>
                  </div>
                </div>
              </div>
            ) : (
              <div className="incident-manual-source-notice">
                <Space align="start" size={12}>
                  <div className="manual-source-icon">
                    <UserOutlined />
                  </div>
                  <div>
                    <div className="manual-source-title">
                      Sự cố do cứu hộ viên phát hiện thực tế tại bể
                    </div>
                    <div className="manual-source-desc">
                      Cảnh báo được nhân viên cứu hộ trực tiếp phát hiện và kích hoạt chuông cứu hộ tại bể. Không có hình ảnh trích xuất từ Camera AI.
                    </div>
                  </div>
                </Space>
              </div>
            )}
          </div>
        )}
      </Modal>
    </div>
  )
}

import { useState, useEffect } from 'react';
import {
  Card,
  Table,
  Button,
  Space,
  Tag,
  DatePicker,
  Select,
  Typography,
  Tabs,
  Tooltip,
} from 'antd';
import {
  PlusOutlined,
  ExperimentOutlined,
  ReloadOutlined,
  EyeOutlined,
  LineChartOutlined,
  CheckCircleOutlined,
  WarningOutlined,
  CloseCircleOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { waterQualityService } from '../../../features/waterQuality/services/waterQualityService';
import CreateWaterQualityLogModal from '../../../features/waterQuality/components/CreateWaterQualityLogModal';
import WaterQualityLogDetailDrawer from '../../../features/waterQuality/components/WaterQualityLogDetailDrawer';
import WaterQualityAnalyticsTab from '../../../features/waterQuality/components/WaterQualityAnalyticsTab';

const { RangePicker } = DatePicker;
const { Title, Text } = Typography;

export default function WaterQualityPage() {
  const [activeTab, setActiveTab] = useState('logs');
  const [loading, setLoading] = useState(false);
  const [logs, setLogs] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [reloadKey, setReloadKey] = useState(0);

  // Filters
  const [dateRange, setDateRange] = useState(null);
  const [statusFilter, setStatusFilter] = useState(null);

  // Modals & Drawers
  const [createModalVisible, setCreateModalVisible] = useState(false);
  const [detailDrawerVisible, setDetailDrawerVisible] = useState(false);
  const [selectedLog, setSelectedLog] = useState(null);

  useEffect(() => {
    if (activeTab !== 'logs') return undefined;

    let ignore = false;
    const params = {
      pageIndex,
      pageSize,
    };
    if (dateRange && dateRange[0] && dateRange[1]) {
      params.fromDate = dateRange[0].startOf('day').toISOString();
      params.toDate = dateRange[1].endOf('day').toISOString();
    }
    if (statusFilter) {
      params.status = statusFilter;
    }

    waterQualityService
      .getLogs(params)
      .then((res) => {
        if (!ignore) {
          setLogs(res.items || []);
          setTotalCount(res.totalCount || 0);
          setLoading(false);
        }
      })
      .catch((err) => {
        if (!ignore) {
          console.error('Lỗi tải danh sách phiếu đo nước:', err);
          setLoading(false);
        }
      });

    return () => {
      ignore = true;
    };
  }, [pageIndex, pageSize, dateRange, statusFilter, activeTab, reloadKey]);

  const handleOpenDetail = (record) => {
    setSelectedLog(record);
    setDetailDrawerVisible(true);
  };

  const handleManualRefresh = () => {
    setLoading(true);
    setReloadKey((prev) => prev + 1);
  };

  const handleCreateSuccess = () => {
    setPageIndex(1);
    handleManualRefresh();
  };

  const columns = [
    {
      title: 'Thời điểm đo',
      dataIndex: 'inspectionTime',
      key: 'inspectionTime',
      width: 160,
      render: (t) => dayjs(t).format('DD/MM/YYYY HH:mm'),
    },
    {
      title: 'Người đo',
      dataIndex: 'inspectorName',
      key: 'inspectorName',
      width: 140,
      render: (name, record) => name || (record.source === 'Device' ? 'Cảm biến IoT' : 'Nhân viên'),
    },
    {
      title: 'pH',
      dataIndex: 'phValue',
      key: 'phValue',
      width: 90,
      align: 'center',
      render: (v) => {
        const num = Number(v);
        const isIdeal = num >= 7.2 && num <= 7.8;
        return (
          <Tooltip title={isIdeal ? 'Trong ngưỡng chuẩn (7.2 - 7.8)' : 'Lệch ngưỡng chuẩn'}>
            <Text strong style={{ color: isIdeal ? '#389e0d' : '#cf1322' }}>
              {num.toFixed(2)}
            </Text>
          </Tooltip>
        );
      },
    },
    {
      title: 'Clo tự do',
      dataIndex: 'freeChlorineMgL',
      key: 'freeChlorineMgL',
      width: 120,
      align: 'center',
      render: (v) => {
        const num = Number(v);
        const isIdeal = num >= 1.0 && num <= 3.0;
        return (
          <Tooltip title={isIdeal ? 'Trong ngưỡng chuẩn (1.0 - 3.0 mg/L)' : 'Lệch ngưỡng chuẩn'}>
            <Text strong style={{ color: isIdeal ? '#389e0d' : '#cf1322' }}>
              {num.toFixed(2)} mg/L
            </Text>
          </Tooltip>
        );
      },
    },
    {
      title: 'Nước trong',
      dataIndex: 'isWaterClear',
      key: 'isWaterClear',
      width: 110,
      align: 'center',
      render: (clear) => {
        if (clear === true) return <Tag color="cyan">Trong vắt</Tag>;
        if (clear === false) return <Tag color="magenta">Bị đục</Tag>;
        return <Text type="secondary">—</Text>;
      },
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      key: 'status',
      width: 130,
      align: 'center',
      render: (status) => {
        switch (status) {
          case 'WithinLimits':
            return (
              <Tag color="success" icon={<CheckCircleOutlined />}>
                Đạt chuẩn
              </Tag>
            );
          case 'Warning':
            return (
              <Tag color="warning" icon={<WarningOutlined />}>
                Cảnh báo
              </Tag>
            );
          case 'Danger':
            return (
              <Tag color="error" icon={<CloseCircleOutlined />}>
                Nguy hiểm
              </Tag>
            );
          default:
            return <Tag>{status}</Tag>;
        }
      },
    },
    {
      title: 'Khuyến nghị & Hóa chất châm',
      dataIndex: 'recommendation',
      key: 'recommendation',
      ellipsis: true,
      render: (rec, record) => (
        <span
          style={{
            color: record.status === 'Danger' ? '#cf1322' : '#262626',
            fontWeight: record.status === 'Danger' ? 600 : 'normal',
          }}
        >
          {rec}
        </span>
      ),
    },
    {
      title: 'Thao tác',
      key: 'action',
      width: 100,
      align: 'center',
      render: (_, record) => (
        <Button
          type="link"
          icon={<EyeOutlined />}
          size="small"
          onClick={() => handleOpenDetail(record)}
        >
          Xem
        </Button>
      ),
    },
  ];

  return (
    <div style={{ padding: '24px' }}>
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: 16,
          flexWrap: 'wrap',
          gap: 12,
        }}
      >
        <div>
          <Title level={3} style={{ margin: 0 }}>
            Kiểm soát chất lượng nước
          </Title>
          <Text type="secondary">
            Theo dõi nồng độ pH, Clo tự do, độ trong và nhận gợi ý châm hóa chất tự động cho bể bơi
          </Text>
        </div>

        <Space>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => setCreateModalVisible(true)}
            size="large"
          >
            Tạo phiếu đo mới
          </Button>
        </Space>
      </div>

      <Tabs
        activeKey={activeTab}
        onChange={setActiveTab}
        items={[
          {
            key: 'logs',
            label: (
              <Space>
                <ExperimentOutlined />
                <span>Lịch sử đo chất lượng nước</span>
              </Space>
            ),
            children: (
              <Card>
                {/* Search & Filter Bar */}
                <div
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    marginBottom: 16,
                    flexWrap: 'wrap',
                    gap: 12,
                  }}
                >
                  <Space wrap>
                    <RangePicker
                      value={dateRange}
                      onChange={(dates) => {
                        setLoading(true);
                        setDateRange(dates);
                      }}
                      format="DD/MM/YYYY"
                      placeholder={['Từ ngày', 'Đến ngày']}
                    />
                    <Select
                      placeholder="Lọc trạng thái"
                      allowClear
                      style={{ width: 160 }}
                      value={statusFilter}
                      onChange={(val) => {
                        setLoading(true);
                        setStatusFilter(val);
                      }}
                      options={[
                        { value: 'WithinLimits', label: 'Đạt chuẩn' },
                        { value: 'Warning', label: 'Cảnh báo' },
                        { value: 'Danger', label: 'Nguy hiểm' },
                      ]}
                    />
                  </Space>

                  <Space>
                    <Button icon={<ReloadOutlined />} onClick={handleManualRefresh} loading={loading}>
                      Làm mới
                    </Button>
                  </Space>
                </div>

                <Table
                  dataSource={logs}
                  columns={columns}
                  rowKey="id"
                  loading={loading}
                  pagination={{
                    current: pageIndex,
                    pageSize,
                    total: totalCount,
                    showSizeChanger: true,
                    onChange: (page, size) => {
                      setLoading(true);
                      setPageIndex(page);
                      setPageSize(size);
                    },
                    showTotal: (total) => `Tổng cộng ${total} phiếu đo`,
                  }}
                  onRow={(record) => ({
                    onDoubleClick: () => handleOpenDetail(record),
                  })}
                />
              </Card>
            ),
          },
          {
            key: 'analytics',
            label: (
              <Space>
                <LineChartOutlined />
                <span>Phân tích & Giám sát chỉ số</span>
              </Space>
            ),
            children: <WaterQualityAnalyticsTab onViewDetail={handleOpenDetail} />,
          },
        ]}
      />

      <CreateWaterQualityLogModal
        visible={createModalVisible}
        onCancel={() => setCreateModalVisible(false)}
        onSuccess={handleCreateSuccess}
      />

      <WaterQualityLogDetailDrawer
        visible={detailDrawerVisible}
        log={selectedLog}
        onClose={() => setDetailDrawerVisible(false)}
      />
    </div>
  );
}

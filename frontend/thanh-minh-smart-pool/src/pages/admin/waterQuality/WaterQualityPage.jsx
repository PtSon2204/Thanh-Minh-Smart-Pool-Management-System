import { useState, useEffect, useRef } from 'react';
import {
  Alert,
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
  message,
} from 'antd';
import { useQueryClient } from '@tanstack/react-query';
import {
  ExperimentOutlined,
  ReloadOutlined,
  EyeOutlined,
  LineChartOutlined,
  ScheduleOutlined,
  CheckCircleOutlined,
  WarningOutlined,
  CloseCircleOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { waterQualityService } from '../../../features/waterQuality/services/waterQualityService';
import CreateWaterQualityLogModal from '../../../features/waterQuality/components/CreateWaterQualityLogModal';
import WaterQualityLogDetailDrawer from '../../../features/waterQuality/components/WaterQualityLogDetailDrawer';
import WaterQualityAnalyticsTab from '../../../features/waterQuality/components/WaterQualityAnalyticsTab';
import PoolDutyScheduleTab from '../../../features/waterQuality/components/PoolDutyScheduleTab';
import { useAuthStore } from '../../../features/auth/store/authStore';
import { useSearchParams } from 'react-router-dom';
import { startSignalRConnection, onWaterQualityUpdated } from '../../../services/signalrClient';
import {
  TODAY_WATER_QUALITY_GATE_QUERY_KEY,
  useTodayWaterQualityGate,
} from '../../../features/waterQuality/hooks/useTodayWaterQualityGate';
import { useWaterPoolSuspension } from '../../../features/waterQuality/hooks/useWaterPoolSuspension';

const { RangePicker } = DatePicker;
const { Title, Text } = Typography;

export default function WaterQualityPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const linkedLogId = searchParams.get('logId');
  const queryClient = useQueryClient();
  const role = useAuthStore((state) => state.session?.role?.toUpperCase());
  const isAdmin = role === 'ADMIN';
  const isLifeguard = role === 'LIFEGUARD';
  const waterGate = useTodayWaterQualityGate();
  const mustMeasureWater = isLifeguard && waterGate.mustMeasureWater;
  const autoOpenedGateRef = useRef(false);
  const [activeTab, setActiveTab] = useState(() => (role === 'ADMIN' && !linkedLogId ? 'analytics' : 'logs'));
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
  const [latestLog, setLatestLog] = useState(null);
  const lastLocalUpdateIdRef = useRef(null);
  const {
    liveStatus,
    isPoolMaintenance,
    updatingPool,
    confirmSuspendPool,
    confirmResumePool,
  } = useWaterPoolSuspension({ enabled: isAdmin });

  useEffect(() => {
    if (!mustMeasureWater) {
      if (!waterGate.isCheckingGate) {
        autoOpenedGateRef.current = false;
      }
      return undefined;
    }

    if (autoOpenedGateRef.current) {
      return undefined;
    }

    const timer = setTimeout(() => {
      autoOpenedGateRef.current = true;
      setActiveTab('logs');
      setCreateModalVisible(true);
    }, 0);

    return () => clearTimeout(timer);
  }, [mustMeasureWater, waterGate.isCheckingGate]);

  useEffect(() => {
    startSignalRConnection();
    const unsub = onWaterQualityUpdated((payload) => {
      if (payload?.id && lastLocalUpdateIdRef.current === payload.id) {
        lastLocalUpdateIdRef.current = null;
        return;
      }
      setReloadKey((prev) => prev + 1);
    });
    return () => unsub();
  }, []);

  useEffect(() => {
    if (!linkedLogId) return undefined;
    let stale = false;
    waterQualityService.getLogById(linkedLogId)
      .then((log) => {
        if (!stale) {
          setSelectedLog(log);
          setDetailDrawerVisible(true);
        }
      })
      .catch((error) => {
        if (!stale) message.error(error?.response?.data?.message || 'Không thể mở kết quả đo.');
      });
    return () => { stale = true; };
  }, [linkedLogId]);

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

  useEffect(() => {
    if (!isAdmin) return undefined;
    let ignore = false;
    waterQualityService
      .getLogs({ pageIndex: 1, pageSize: 1 })
      .then((res) => {
        if (!ignore) {
          setLatestLog(res?.items?.[0] || null);
        }
      })
      .catch(() => {});
    return () => {
      ignore = true;
    };
  }, [isAdmin, reloadKey]);

  const handleOpenDetail = (record) => {
    setSelectedLog(record);
    setDetailDrawerVisible(true);
  };

  const handleOpenLogById = async (logId) => {
    if (!logId) return;
    try {
      const log = await waterQualityService.getLogById(logId);
      setSelectedLog(log);
      setDetailDrawerVisible(true);
    } catch (error) {
      message.error(error?.response?.data?.message || 'Không thể mở chi tiết phiếu đo.');
    }
  };

  const handleManualRefresh = () => {
    setLoading(true);
    setReloadKey((prev) => prev + 1);
  };

  const handleCreateSuccess = async (createdLog) => {
    const wasGated = mustMeasureWater;
    if (createdLog?.id) {
      lastLocalUpdateIdRef.current = createdLog.id;
    }
    setPageIndex(1);
    handleManualRefresh();
    await queryClient.invalidateQueries({
      queryKey: [TODAY_WATER_QUALITY_GATE_QUERY_KEY],
    });
    if (wasGated) {
      message.info('Đã hoàn thành đo chất lượng nước hôm nay! Bạn đã có thể truy cập các chức năng khác.');
    }
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
        <Space direction="vertical" size={2}>
          <span
            style={{
              color: record.status === 'Danger' ? '#cf1322' : '#262626',
              fontWeight: record.status === 'Danger' ? 600 : 'normal',
            }}
          >
            {rec}
          </span>
          {record.threeDayTrendDetected && record.generatedNextDayDutyDate && (
            <Tag color="volcano" style={{ width: 'fit-content', margin: 0 }}>
              Xu hướng 3 ngày đi xuống → Đã tạo lịch trực bể ngày{' '}
              {dayjs(record.generatedNextDayDutyDate).format('DD/MM')}
            </Tag>
          )}
          {record.threeDayTrendDetected && !record.generatedNextDayDutyDate && (
            <Tag color="red" style={{ width: 'fit-content', margin: 0 }}>
              Xu hướng 3 ngày đi xuống · Xử lý ngay trong ngày
            </Tag>
          )}
        </Space>
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
            {isAdmin ? 'Thống kê chất lượng nước' : 'Kiểm soát chất lượng nước'}
          </Title>
          <Text type="secondary">
            {isAdmin
              ? 'Theo dõi thống kê xu hướng chỉ số pH, Clo tự do, lịch sử đo và cảnh báo chất lượng nước'
              : 'Theo dõi nồng độ pH, Clo tự do, nhận gợi ý châm hóa chất và quản lý lịch trực bể định kỳ'}
          </Text>
        </div>
      </div>

      {isAdmin && isPoolMaintenance && (
        <Alert
          type={latestLog?.status === 'WithinLimits' ? 'success' : 'warning'}
          showIcon
          title={
            latestLog?.status === 'WithinLimits'
              ? 'Chất lượng nước đã đạt chuẩn — Bể bơi hiện vẫn đang tạm đóng cửa'
              : 'Bể bơi đang tạm đóng cửa / bảo trì đón khách'
          }
          description={
            liveStatus?.statusReason ||
            'Hệ thống đang khóa đặt vé online và hiển thị cảnh báo bảo trì tại trang chủ.'
          }
          action={
            <Space wrap>
              {latestLog?.id && (
                <Button size="small" onClick={() => handleOpenDetail(latestLog)}>
                  Xem phiếu đo mới nhất
                </Button>
              )}
              <Button
                type="primary"
                size="small"
                loading={updatingPool}
                onClick={confirmResumePool}
                style={{ backgroundColor: '#16a34a', borderColor: '#16a34a' }}
              >
                Mở lại hoạt động bể
              </Button>
            </Space>
          }
          style={{ marginBottom: 16 }}
        />
      )}

      {isAdmin && !isPoolMaintenance && latestLog?.status === 'Danger' && (
        <Alert
          type="error"
          showIcon
          title="Cảnh báo khẩn: Chỉ số nước mới nhất ở mức NGUY HIỂM — Admin cần xem xét đóng cửa bể"
          description={`Phiếu đo lúc ${dayjs(latestLog.inspectionTime).format('DD/MM/YYYY HH:mm')} ghi nhận pH ${Number(latestLog.phValue).toFixed(2)}, Clo ${Number(latestLog.freeChlorineMgL).toFixed(2)} mg/L. Hãy kiểm tra và quyết định tạm đóng cửa đón khách nếu cần thiết để xử lý an toàn.`}
          action={
            <Space wrap>
              <Button size="small" onClick={() => handleOpenDetail(latestLog)}>
                Xem chi tiết
              </Button>
              <Button
                type="primary"
                danger
                size="small"
                loading={updatingPool}
                onClick={() => confirmSuspendPool(latestLog)}
              >
                Tạm đóng cửa bể
              </Button>
            </Space>
          }
          style={{ marginBottom: 16 }}
        />
      )}

      {mustMeasureWater && (
        <Alert
          type="warning"
          showIcon
          title="Nhiệm vụ bắt buộc đầu ngày: Đo chất lượng nước"
          description={
            waterGate.data?.reason ||
            'Bạn được phân công lịch trực/đo nước hôm nay. Vui lòng hoàn thành đo chất lượng nước trước khi sử dụng các chức năng làm việc khác.'
          }
          action={
            <Button
              type="primary"
              size="small"
              icon={<ExperimentOutlined />}
              onClick={() => setCreateModalVisible(true)}
            >
              Đo nước ngay
            </Button>
          }
          style={{ marginBottom: 16 }}
        />
      )}

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
            children: <WaterQualityAnalyticsTab reloadKey={reloadKey} onViewDetail={handleOpenDetail} />,
          },
          {
            key: 'duty-schedule',
            label: (
              <Space>
                <ScheduleOutlined />
                <span>Lịch trực bể</span>
              </Space>
            ),
            children: (
              <PoolDutyScheduleTab
                reloadKey={reloadKey}
                readOnly={!isLifeguard}
                isAdmin={isAdmin}
                onViewLogById={handleOpenLogById}
                onOpenCreateMeasurement={isLifeguard ? () => setCreateModalVisible(true) : undefined}
              />
            ),
          },
        ]}
      />

      {isLifeguard && (
        <CreateWaterQualityLogModal
          visible={createModalVisible}
          onCancel={() => setCreateModalVisible(false)}
          onSuccess={handleCreateSuccess}
        />
      )}

      <WaterQualityLogDetailDrawer
        visible={detailDrawerVisible}
        log={selectedLog}
        isAdmin={isAdmin}
        onClose={() => {
          setDetailDrawerVisible(false);
          setSelectedLog(null);
          if (linkedLogId) {
            const nextSearchParams = new URLSearchParams(searchParams);
            nextSearchParams.delete('logId');
            setSearchParams(nextSearchParams, { replace: true });
          }
        }}
      />
    </div>
  );
}

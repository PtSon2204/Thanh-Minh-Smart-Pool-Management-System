import { useState, useEffect, useMemo } from 'react';
import {
  Card,
  Button,
  Space,
  Tag,
  Table,
  Select,
  Segmented,
  Typography,
  Row,
  Col,
  Statistic,
  Modal,
  Form,
  message,
} from 'antd';
import {
  ReloadOutlined,
  LeftOutlined,
  RightOutlined,
  CalendarOutlined,
  UnorderedListOutlined,
  UserOutlined,
  CheckCircleOutlined,
  ClockCircleOutlined,
  ExperimentOutlined,
  EyeOutlined,
  WarningOutlined,
  UserAddOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { waterQualityService } from '../services/waterQualityService';
import { useAuthStore } from '../../auth/store/authStore';

const { Text } = Typography;

const WEEKDAY_LABELS = [
  'Thứ 2',
  'Thứ 3',
  'Thứ 4',
  'Thứ 5',
  'Thứ 6',
  'Thứ 7',
  'Chủ nhật',
];

const MEASUREMENT_STATUS_META = {
  WithinLimits: { color: 'green', label: 'Đạt chuẩn' },
  Warning: { color: 'orange', label: 'Cảnh báo' },
  Danger: { color: 'red', label: 'Nguy hiểm' },
};

function getMonday(d) {
  const date = dayjs(d);
  const day = date.day();
  const diff = day === 0 ? -6 : 1 - day;
  return date.add(diff, 'day').startOf('day');
}

function renderStatusTag(status, isTrendDuty = false) {
  switch (status) {
    case 'Completed':
      return (
        <Tag color="success" icon={<CheckCircleOutlined />}>
          {isTrendDuty ? 'Đã xử lý' : 'Đã đo (1/1)'}
        </Tag>
      );
    case 'Unassigned':
      return (
        <Tag color="warning" icon={<WarningOutlined />}>
          Chờ Admin phân công
        </Tag>
      );
    case 'InProgress':
      return (
        <Tag color="processing" icon={<ClockCircleOutlined />}>
          {isTrendDuty ? 'Đang xử lý' : 'Đang trực · Chưa đo'}
        </Tag>
      );
    case 'Scheduled':
      return <Tag color="blue">{isTrendDuty ? 'Đã phân công xử lý' : 'Đã phân công'}</Tag>;
    default:
      return <Tag>{status}</Tag>;
  }
}

export default function PoolDutyScheduleTab({
  onOpenCreateMeasurement,
  onViewLogById,
  readOnly = false,
  isAdmin = false,
  reloadKey: externalReloadKey = 0,
}) {
  const session = useAuthStore((state) => state.session);
  const currentUserId = session?.id ?? session?.userId;
  const [viewMode, setViewMode] = useState('board');
  const [weekStart, setWeekStart] = useState(() => getMonday(dayjs()));
  const [loading, setLoading] = useState(false);
  const [schedules, setSchedules] = useState([]);
  const [options, setOptions] = useState({ staffs: [], shifts: [] });
  const [reloadKey, setReloadKey] = useState(0);

  // Filters
  const [employeeFilter, setEmployeeFilter] = useState(null);
  const [statusFilter, setStatusFilter] = useState(null);

  // Admin assign modal for 3-day trend duty
  const [assignTarget, setAssignTarget] = useState(null);
  const [assignSaving, setAssignSaving] = useState(false);
  const [completingId, setCompletingId] = useState(null);
  const [assignForm] = Form.useForm();

  const todayStr = dayjs().format('YYYY-MM-DD');

  const weekDates = useMemo(() => {
    return Array.from({ length: 7 }, (_, idx) => weekStart.add(idx, 'day'));
  }, [weekStart]);

  useEffect(() => {
    waterQualityService
      .getDutyScheduleOptions()
      .then((res) => setOptions(res || { staffs: [], shifts: [] }))
      .catch((err) => console.error('Lỗi tải danh sách nhân sự trực bể:', err));
  }, []);

  useEffect(() => {
    let ignore = false;

    const params = {
      fromDate: weekStart.format('YYYY-MM-DD'),
      toDate: weekStart.add(6, 'day').format('YYYY-MM-DD'),
      pageIndex: 1,
      pageSize: 100,
    };

    if (employeeFilter) params.employeeId = employeeFilter;
    if (statusFilter) params.status = statusFilter;

    waterQualityService
      .getDutySchedules(params)
      .then((res) => {
        if (!ignore) {
          setSchedules(res.items || []);
          setLoading(false);
        }
      })
      .catch((err) => {
        if (!ignore) {
          console.error('Lỗi tải lịch trực bể:', err);
          setLoading(false);
        }
      });

    return () => {
      ignore = true;
    };
  }, [weekStart, employeeFilter, statusFilter, reloadKey, externalReloadKey]);

  const handleRefresh = () => {
    setLoading(true);
    setReloadKey((k) => k + 1);
  };

  const openAssignModal = (duty) => {
    setAssignTarget(duty);
    assignForm.setFieldsValue({
      assignedEmployeeId: duty.assignedEmployeeId || undefined,
      shiftId: duty.shiftId || undefined,
    });
  };

  const handleAssignSubmit = async (values) => {
    if (!assignTarget) return;
    setAssignSaving(true);
    try {
      await waterQualityService.assignTrendDutySchedule(assignTarget.id, {
        assignedEmployeeId: values.assignedEmployeeId,
        shiftId: values.shiftId || null,
      });
      message.success('Đã phân công nhân viên thực hiện Lịch trực bể!');
      setAssignTarget(null);
      assignForm.resetFields();
      handleRefresh();
    } catch (error) {
      message.error(error?.response?.data?.message || 'Không thể phân công lịch trực bể.');
    } finally {
      setAssignSaving(false);
    }
  };

  const handleCompleteTrendDuty = async (duty) => {
    setCompletingId(duty.id);
    try {
      await waterQualityService.completeTrendDutySchedule(duty.id, {});
      message.success('Đã xác nhận hoàn thành xử lý Lịch trực bể!');
      handleRefresh();
    } catch (error) {
      message.error(error?.response?.data?.message || 'Không thể hoàn thành lịch trực bể.');
    } finally {
      setCompletingId(null);
    }
  };

  // Tách 2 lớp lịch: (1) Lịch đo hằng ngày đồng bộ Lịch làm việc, (2) Lịch trực bể phát sinh do xu hướng 3 ngày
  const { dailySummaryMap, dailyRows, trendDutyByDateMap, trendRows, combinedRows } = useMemo(() => {
    const shiftMap = new Map();
    const trendMap = new Map();
    const trends = [];

    schedules.forEach((item) => {
      if (item.isTrendDuty) {
        trends.push(item);
        const existingTrend = trendMap.get(item.dutyDate);
        if (!existingTrend || existingTrend.status === 'Completed') {
          trendMap.set(item.dutyDate, item);
        }
        return;
      }

      const existing = shiftMap.get(item.dutyDate);
      if (!existing) {
        shiftMap.set(item.dutyDate, {
          ...item,
          staffNames: item.assignedEmployeeName ? [item.assignedEmployeeName] : [],
        });
      } else {
        if (
          item.assignedEmployeeName &&
          !existing.staffNames.includes(item.assignedEmployeeName)
        ) {
          existing.staffNames.push(item.assignedEmployeeName);
        }
        if (!existing.waterQualityLogId && item.waterQualityLogId) {
          existing.waterQualityLogId = item.waterQualityLogId;
          existing.measuredPh = item.measuredPh;
          existing.measuredChlorine = item.measuredChlorine;
          existing.measurementStatus = item.measurementStatus;
          existing.measuredAt = item.measuredAt;
          existing.status = item.status;
          existing.notes = item.notes;
        } else if (existing.status === 'Scheduled' && item.status === 'InProgress') {
          existing.status = 'InProgress';
        }
      }
    });

    const dRows = Array.from(shiftMap.values()).sort((a, b) =>
      a.dutyDate.localeCompare(b.dutyDate)
    );
    const tRows = trends.sort((a, b) => a.dutyDate.localeCompare(b.dutyDate));
    const combined = [
      ...tRows.map((t) => ({
        ...t,
        rowKey: `trend-${t.id}`,
        staffNames: [t.assignedEmployeeName || 'Chờ Admin phân công'],
      })),
      ...dRows.map((d) => ({
        ...d,
        rowKey: `daily-${d.dutyDate}`,
      })),
    ].sort((a, b) => a.dutyDate.localeCompare(b.dutyDate) || (a.isTrendDuty ? -1 : 1));

    return {
      dailySummaryMap: shiftMap,
      dailyRows: dRows,
      trendDutyByDateMap: trendMap,
      trendRows: tRows,
      combinedRows: combined,
    };
  }, [schedules]);

  const totalDutyDays = dailyRows.length;
  const completedDutyDays = dailyRows.filter((d) => Boolean(d.waterQualityLogId)).length;
  const pendingTrendCount = trendRows.filter((t) => t.status === 'Unassigned').length;

  const renderMeasurementMeta = (record) => {
    if (!record?.waterQualityLogId && record?.measuredPh == null) return null;
    const meta = MEASUREMENT_STATUS_META[record.measurementStatus] || {
      color: 'default',
      label: record.measurementStatus || '',
    };
    return (
      <Space size={4} wrap>
        <Text style={{ fontSize: 12 }}>
          pH <strong>{Number(record.measuredPh).toFixed(2)}</strong> | Clo{' '}
          <strong>{Number(record.measuredChlorine).toFixed(2)}</strong>
        </Text>
        <Tag color={meta.color} style={{ margin: 0 }}>
          {meta.label}
        </Tag>
      </Space>
    );
  };

  const canLifeguardCompleteTrend = (duty) =>
    !readOnly &&
    !isAdmin &&
    Boolean(currentUserId) &&
    duty.status !== 'Unassigned' &&
    duty.status !== 'Completed' &&
    duty.status !== 'Cancelled' &&
    Boolean(duty.assignedEmployeeId) &&
    duty.assignedEmployeeId === currentUserId;

  const tableColumns = [
    {
      title: 'Ngày trực',
      dataIndex: 'dutyDate',
      key: 'dutyDate',
      width: 140,
      render: (d) => (
        <Space size={6}>
          <Text strong={d === todayStr}>{dayjs(d).format('DD/MM/YYYY')}</Text>
          {d === todayStr && <Tag color="blue">Hôm nay</Tag>}
        </Space>
      ),
    },
    {
      title: 'Loại lịch',
      key: 'dutyType',
      width: 170,
      render: (_, record) =>
        record.isTrendDuty ? (
          <Tag color="volcano" icon={<WarningOutlined />}>
            Xử lý xu hướng 3 ngày
          </Tag>
        ) : (
          <Tag color="blue">Đo nước trong ngày</Tag>
        ),
    },
    {
      title: 'Nhân viên phụ trách',
      key: 'staffNames',
      width: 210,
      render: (_, record) => (
        <Space>
          <UserOutlined style={{ color: record.status === 'Unassigned' ? '#faad14' : '#1890ff' }} />
          <div>
            <Text strong type={record.status === 'Unassigned' ? 'warning' : undefined}>
              {record.staffNames.join(', ')}
            </Text>
            {record.shiftName && record.isTrendDuty && record.assignedEmployeeId && (
              <div>
                <Text type="secondary" style={{ fontSize: 11 }}>{record.shiftName}</Text>
              </div>
            )}
          </div>
        </Space>
      ),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      key: 'status',
      width: 170,
      align: 'center',
      render: (status, record) => renderStatusTag(status, record.isTrendDuty),
    },
    {
      title: 'Kết quả / Hóa chất đề xuất',
      key: 'measurement',
      width: 300,
      render: (_, record) => {
        if (record.isTrendDuty) {
          return (
            <Space direction="vertical" size={4}>
              {record.trendSummary && <Text style={{ fontSize: 12 }}>{record.trendSummary}</Text>}
              {record.suggestedChemicals?.length > 0 && (
                <Space size={[4, 4]} wrap>
                  {record.suggestedChemicals.map((chem, idx) => (
                    <Tag color="geekblue" key={`tbl-chem-${idx}`}>{chem}</Tag>
                  ))}
                </Space>
              )}
              {record.triggerWaterQualityLogId && onViewLogById && (
                <Button
                  type="link"
                  size="small"
                  icon={<EyeOutlined />}
                  style={{ padding: 0, height: 'auto' }}
                  onClick={() => onViewLogById(record.triggerWaterQualityLogId)}
                >
                  Xem phiếu kích hoạt
                </Button>
              )}
            </Space>
          );
        }
        if (record.waterQualityLogId) {
          return (
            <Space direction="vertical" size={2}>
              {renderMeasurementMeta(record)}
              {onViewLogById && (
                <Button
                  type="link"
                  size="small"
                  icon={<EyeOutlined />}
                  style={{ padding: 0, height: 'auto' }}
                  onClick={() => onViewLogById(record.waterQualityLogId)}
                >
                  Xem phiếu đo
                </Button>
              )}
            </Space>
          );
        }
        if (
          !readOnly &&
          record.dutyDate === todayStr &&
          onOpenCreateMeasurement &&
          (!record.assignedEmployeeId || record.assignedEmployeeId === currentUserId)
        ) {
          return (
            <Button
              type="primary"
              size="small"
              icon={<ExperimentOutlined />}
              onClick={() => onOpenCreateMeasurement()}
            >
              Đo nước ngay
            </Button>
          );
        }
        return <Text type="secondary">Chưa có phiếu đo</Text>;
      },
    },
    {
      title: 'Thao tác',
      key: 'actions',
      width: 180,
      render: (_, record) => {
        if (!record.isTrendDuty) return null;
        return (
          <Space wrap>
            {isAdmin && record.status !== 'Completed' && record.status !== 'Cancelled' && (
              <Button
                type="primary"
                size="small"
                icon={<UserAddOutlined />}
                onClick={() => openAssignModal(record)}
              >
                {record.assignedEmployeeId ? 'Đổi người' : 'Phân công'}
              </Button>
            )}
            {canLifeguardCompleteTrend(record) && (
              <Button
                type="primary"
                size="small"
                loading={completingId === record.id}
                onClick={() => handleCompleteTrendDuty(record)}
              >
                Hoàn thành xử lý
              </Button>
            )}
          </Space>
        );
      },
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {/* Overview KPI row */}
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={8}>
          <Card size="small">
            <Statistic
              title="Ngày có lịch đo trong tuần"
              value={totalDutyDays}
              suffix="/ 7 ngày"
              prefix={<CalendarOutlined style={{ color: '#1890ff' }} />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card size="small">
            <Statistic
              title="Ngày đã đo nước (1 lần/ngày)"
              value={completedDutyDays}
              suffix={`/ ${totalDutyDays} ngày`}
              styles={{ content: { color: '#3f8600' } }}
              prefix={<CheckCircleOutlined style={{ color: '#52c41a' }} />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card size="small">
            <Statistic
              title="Lịch trực phát sinh (Xu hướng 3 ngày)"
              value={trendRows.length}
              suffix={pendingTrendCount > 0 ? `(${pendingTrendCount} chờ phân công)` : 'lịch trong tuần'}
              styles={{ content: { color: pendingTrendCount > 0 ? '#d46b08' : '#1890ff' } }}
              prefix={<WarningOutlined style={{ color: pendingTrendCount > 0 ? '#faad14' : '#1890ff' }} />}
            />
          </Card>
        </Col>
      </Row>

      <Card>
        {/* Top Toolbar */}
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: 12,
            marginBottom: 16,
          }}
        >
          <Space wrap>
            <Button
              icon={<LeftOutlined />}
              onClick={() => {
                setLoading(true);
                setWeekStart((w) => w.subtract(7, 'day'));
              }}
            />
            <Button
              onClick={() => {
                setLoading(true);
                setWeekStart(getMonday(dayjs()));
              }}
            >
              Tuần hiện tại
            </Button>
            <Button
              icon={<RightOutlined />}
              onClick={() => {
                setLoading(true);
                setWeekStart((w) => w.add(7, 'day'));
              }}
            />
            <Text strong style={{ marginLeft: 4 }}>
              Tuần: {weekStart.format('DD/MM/YYYY')} –{' '}
              {weekStart.add(6, 'day').format('DD/MM/YYYY')}
            </Text>

            <Select
              allowClear
              placeholder="Lọc nhân viên trực"
              style={{ width: 200 }}
              value={employeeFilter}
              onChange={(val) => {
                setLoading(true);
                setEmployeeFilter(val);
              }}
              options={(options.staffs || []).map((st) => ({
                value: st.employeeId,
                label: st.fullName,
              }))}
            />

            <Select
              allowClear
              placeholder="Trạng thái"
              style={{ width: 190 }}
              value={statusFilter}
              onChange={(val) => {
                setLoading(true);
                setStatusFilter(val);
              }}
              options={[
                { value: 'Unassigned', label: 'Chờ Admin phân công' },
                { value: 'Scheduled', label: 'Đã lên lịch / phân công' },
                { value: 'InProgress', label: 'Đang trực / xử lý' },
                { value: 'Completed', label: 'Đã hoàn thành' },
              ]}
            />
          </Space>

          <Space wrap>
            <Segmented
              value={viewMode}
              onChange={setViewMode}
              options={[
                {
                  value: 'board',
                  label: (
                    <Space size={4}>
                      <CalendarOutlined />
                      <span>Bảng lịch tuần</span>
                    </Space>
                  ),
                },
                {
                  value: 'table',
                  label: (
                    <Space size={4}>
                      <UnorderedListOutlined />
                      <span>Danh sách</span>
                    </Space>
                  ),
                },
              ]}
            />

            <Button icon={<ReloadOutlined />} onClick={handleRefresh} loading={loading}>
              Làm mới
            </Button>
          </Space>
        </div>

        {/* Weekly Timetable View */}
        {viewMode === 'board' ? (
          <div style={{ overflowX: 'auto' }}>
            <table
              style={{
                width: '100%',
                borderCollapse: 'collapse',
                minWidth: 960,
                border: '1px solid #f0f0f0',
              }}
            >
              <thead>
                <tr style={{ background: '#fafafa' }}>
                  <th
                    style={{
                      padding: '12px',
                      border: '1px solid #f0f0f0',
                      width: 185,
                      textAlign: 'left',
                    }}
                  >
                    Lịch trực bể
                  </th>
                  {weekDates.map((d, idx) => {
                    const isToday = d.isSame(dayjs(), 'day');
                    return (
                      <th
                        key={d.format('YYYY-MM-DD')}
                        style={{
                          padding: '10px 8px',
                          border: '1px solid #f0f0f0',
                          textAlign: 'center',
                          background: isToday ? '#e6f4ff' : '#fafafa',
                        }}
                      >
                        <div style={{ fontWeight: 600, color: isToday ? '#1677ff' : '#262626' }}>
                          {WEEKDAY_LABELS[idx]}
                        </div>
                        <div style={{ fontSize: 12, color: '#8c8c8c' }}>
                          {d.format('DD/MM')}
                        </div>
                      </th>
                    );
                  })}
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td
                    style={{
                      padding: '12px',
                      border: '1px solid #f0f0f0',
                      background: '#f8fafc',
                      verticalAlign: 'middle',
                    }}
                  >
                    <Text strong style={{ fontSize: 13 }}>
                      Đo nước trong ngày
                    </Text>
                  </td>
                  {weekDates.map((d) => {
                    const dateStr = d.format('YYYY-MM-DD');
                    const dayRecord = dailySummaryMap.get(dateStr);
                    const isToday = dateStr === todayStr;
                    const hasMeasured = Boolean(dayRecord?.waterQualityLogId);

                    return (
                      <td
                        key={`daily-${dateStr}`}
                        style={{
                          padding: 8,
                          border: '1px solid #f0f0f0',
                          verticalAlign: 'top',
                          height: 120,
                          background: '#ffffff',
                        }}
                      >
                        {!dayRecord ? (
                          <div
                            style={{
                              height: '100%',
                              minHeight: 104,
                              border: '1px dashed #f0f0f0',
                              borderRadius: 6,
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'center',
                              color: '#bfbfbf',
                              padding: 8,
                            }}
                          >
                            <Text type="secondary" style={{ fontSize: 12 }}>
                              Không có ca trực
                            </Text>
                          </div>
                        ) : (
                          <div
                            onClick={() => {
                              if (hasMeasured) {
                                onViewLogById?.(dayRecord.waterQualityLogId);
                              }
                            }}
                            style={{
                              cursor: hasMeasured && onViewLogById ? 'pointer' : 'default',
                              height: '100%',
                              minHeight: 104,
                              padding: 10,
                              borderRadius: 6,
                              border: hasMeasured
                                ? '1px solid #b7eb8f'
                                : isToday || dayRecord.status === 'Unassigned'
                                  ? '1px solid #ffe58f'
                                  : '1px solid #91caff',
                              background: hasMeasured
                                ? '#f6ffed'
                                : isToday || dayRecord.status === 'Unassigned'
                                  ? '#fffbe6'
                                  : '#f0f5ff',
                              display: 'flex',
                              flexDirection: 'column',
                              justifyContent: 'space-between',
                              gap: 6,
                            }}
                          >
                            <div>
                              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                                <UserOutlined
                                  style={{
                                    color: dayRecord.status === 'Unassigned' ? '#faad14' : '#1890ff',
                                    fontSize: 12,
                                  }}
                                />
                                <Text
                                  strong
                                  type={dayRecord.status === 'Unassigned' ? 'warning' : undefined}
                                  style={{ fontSize: 13 }}
                                >
                                  {dayRecord.staffNames?.length > 0
                                    ? dayRecord.staffNames.join(', ')
                                    : 'Chờ Admin phân công'}
                                </Text>
                              </div>
                            </div>

                            {hasMeasured ? (
                              <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                                <Tag
                                  color="success"
                                  icon={<CheckCircleOutlined />}
                                  style={{ margin: 0, width: 'fit-content' }}
                                >
                                  Đã đo (1/1)
                                </Tag>
                                {renderMeasurementMeta(dayRecord)}
                              </div>
                            ) : isToday ? (
                              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                                <Tag color="warning" style={{ margin: 0, width: 'fit-content' }}>
                                  Chưa đo (0/1)
                                </Tag>
                                {!readOnly &&
                                  onOpenCreateMeasurement &&
                                  (!dayRecord.assignedEmployeeId ||
                                    dayRecord.assignedEmployeeId === currentUserId) && (
                                    <Button
                                      type="primary"
                                      size="small"
                                      icon={<ExperimentOutlined />}
                                      onClick={(e) => {
                                        e.stopPropagation();
                                        onOpenCreateMeasurement();
                                      }}
                                    >
                                      Đo nước ngay
                                    </Button>
                                  )}
                              </div>
                            ) : dayRecord.status === 'Unassigned' ? (
                              <Tag color="warning" style={{ margin: 0, width: 'fit-content' }}>
                                Chưa phân công
                              </Tag>
                            ) : dateStr < todayStr ? (
                              <Tag color="default" style={{ margin: 0, width: 'fit-content' }}>
                                Chưa có phiếu đo
                              </Tag>
                            ) : (
                              <Tag color="blue" style={{ margin: 0, width: 'fit-content' }}>
                                Chờ đến ngày
                              </Tag>
                            )}
                          </div>
                        )}
                      </td>
                    );
                  })}
                </tr>

                {/* Hàng 2: Lịch trực bể phát sinh từ phân tích xu hướng 3 ngày */}
                <tr>
                  <td
                    style={{
                      padding: '12px',
                      border: '1px solid #f0f0f0',
                      background: '#fffbe6',
                      verticalAlign: 'middle',
                    }}
                  >
                    <Space direction="vertical" size={2}>
                      <Text strong style={{ fontSize: 13 }}>
                        Xử lý nước (Xu hướng 3 ngày)
                      </Text>
                      <Text type="secondary" style={{ fontSize: 11 }}>
                        Tự sinh kèm hóa chất đề xuất
                      </Text>
                    </Space>
                  </td>
                  {weekDates.map((d) => {
                    const dateStr = d.format('YYYY-MM-DD');
                    const trendDuty = trendDutyByDateMap.get(dateStr);
                    const isDone = trendDuty?.status === 'Completed';
                    const isUnassigned = trendDuty?.status === 'Unassigned';

                    return (
                      <td
                        key={`trend-${dateStr}`}
                        style={{
                          padding: 8,
                          border: '1px solid #f0f0f0',
                          verticalAlign: 'top',
                          minHeight: 136,
                          background: '#ffffff',
                        }}
                      >
                        {!trendDuty ? (
                          <div
                            style={{
                              height: '100%',
                              minHeight: 120,
                              border: '1px dashed #f0f0f0',
                              borderRadius: 6,
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'center',
                              color: '#bfbfbf',
                              padding: 8,
                              textAlign: 'center',
                            }}
                          >
                            <Text type="secondary" style={{ fontSize: 11 }}>
                              Không phát sinh xử lý
                            </Text>
                          </div>
                        ) : (
                          <div
                            style={{
                              height: '100%',
                              minHeight: 120,
                              padding: 10,
                              borderRadius: 6,
                              border: isDone
                                ? '1px solid #b7eb8f'
                                : isUnassigned
                                  ? '1px solid #ffa940'
                                  : '1px solid #91caff',
                              background: isDone
                                ? '#f6ffed'
                                : isUnassigned
                                  ? '#fff7e6'
                                  : '#e6f4ff',
                              display: 'flex',
                              flexDirection: 'column',
                              justifyContent: 'space-between',
                              gap: 6,
                            }}
                          >
                            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                              {renderStatusTag(trendDuty.status, true)}
                              <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                                <UserOutlined style={{ fontSize: 12, color: isUnassigned ? '#d46b08' : '#1890ff' }} />
                                <Text strong style={{ fontSize: 12 }}>
                                  {trendDuty.assignedEmployeeName || 'Chờ Admin phân công'}
                                </Text>
                              </div>
                              {trendDuty.trendSummary && (
                                <Text type="secondary" style={{ fontSize: 11, lineHeight: 1.35 }}>
                                  {trendDuty.trendSummary}
                                </Text>
                              )}
                              {trendDuty.suggestedChemicals?.length > 0 && (
                                <Space size={[4, 4]} wrap>
                                  {trendDuty.suggestedChemicals.map((chem, idx) => (
                                    <Tag
                                      color="geekblue"
                                      key={`board-chem-${trendDuty.id}-${idx}`}
                                      style={{ fontSize: 10, margin: 0 }}
                                    >
                                      {chem}
                                    </Tag>
                                  ))}
                                </Space>
                              )}
                            </div>

                            <Space direction="vertical" size={4} style={{ width: '100%' }}>
                              {trendDuty.triggerWaterQualityLogId && onViewLogById && (
                                <Button
                                  type="link"
                                  size="small"
                                  style={{ padding: 0, height: 'auto', fontSize: 11 }}
                                  onClick={() => onViewLogById(trendDuty.triggerWaterQualityLogId)}
                                >
                                  Xem phiếu nguồn
                                </Button>
                              )}
                              {isAdmin && !isDone && trendDuty.status !== 'Cancelled' && (
                                <Button
                                  type="primary"
                                  size="small"
                                  block
                                  icon={<UserAddOutlined />}
                                  onClick={() => openAssignModal(trendDuty)}
                                >
                                  {trendDuty.assignedEmployeeId ? 'Đổi người' : 'Phân công'}
                                </Button>
                              )}
                              {canLifeguardCompleteTrend(trendDuty) && (
                                <Button
                                  type="primary"
                                  size="small"
                                  block
                                  loading={completingId === trendDuty.id}
                                  onClick={() => handleCompleteTrendDuty(trendDuty)}
                                >
                                  Hoàn thành xử lý
                                </Button>
                              )}
                            </Space>
                          </div>
                        )}
                      </td>
                    );
                  })}
                </tr>
              </tbody>
            </table>
          </div>
        ) : (
          <Table
            rowKey="rowKey"
            loading={loading}
            dataSource={combinedRows}
            columns={tableColumns}
            pagination={{ pageSize: 14, showSizeChanger: false }}
            locale={{
              emptyText: 'Chưa có lịch trực bể hoặc lịch xử lý phát sinh nào trong tuần này.',
            }}
          />
        )}
      </Card>

      <Modal
        title="Phân công Lịch trực bể (Xu hướng 3 ngày)"
        open={Boolean(assignTarget)}
        onCancel={() => {
          setAssignTarget(null);
          assignForm.resetFields();
        }}
        onOk={() => assignForm.submit()}
        okText="Xác nhận phân công"
        cancelText="Hủy"
        confirmLoading={assignSaving}
        destroyOnHidden
      >
        {assignTarget && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <div>
              <Text strong>Ngày thực hiện: </Text>
              <Text>{dayjs(assignTarget.dutyDate).format('DD/MM/YYYY')}</Text>
            </div>
            {assignTarget.trendSummary && (
              <div>
                <Text strong>Phân tích: </Text>
                <Text>{assignTarget.trendSummary}</Text>
              </div>
            )}
            {assignTarget.suggestedChemicals?.length > 0 && (
              <div>
                <Text strong>Hóa chất đề xuất: </Text>
                <Space size={[4, 4]} wrap style={{ marginTop: 4 }}>
                  {assignTarget.suggestedChemicals.map((chem, idx) => (
                    <Tag color="geekblue" key={`assign-chem-${idx}`}>{chem}</Tag>
                  ))}
                </Space>
              </div>
            )}

            <Form form={assignForm} layout="vertical" onFinish={handleAssignSubmit}>
              <Form.Item
                name="assignedEmployeeId"
                label="Chọn nhân viên cứu hộ phụ trách"
                rules={[{ required: true, message: 'Vui lòng chọn nhân viên cứu hộ.' }]}
              >
                <Select
                  showSearch
                  optionFilterProp="label"
                  placeholder="Chọn nhân viên cứu hộ"
                  options={(options.staffs || []).map((st) => ({
                    value: st.employeeId,
                    label: st.fullName,
                  }))}
                />
              </Form.Item>
              <Form.Item name="shiftId" label="Ca trực ưu tiên (tuỳ chọn)">
                <Select
                  allowClear
                  placeholder="Tự khớp theo ca trực của nhân viên nếu có"
                  options={(options.shifts || []).map((sh) => ({
                    value: sh.shiftId,
                    label: `${sh.name} (${sh.startTime?.slice(0, 5)}–${sh.endTime?.slice(0, 5)})`,
                  }))}
                />
              </Form.Item>
            </Form>
          </div>
        )}
      </Modal>
    </div>
  );
}

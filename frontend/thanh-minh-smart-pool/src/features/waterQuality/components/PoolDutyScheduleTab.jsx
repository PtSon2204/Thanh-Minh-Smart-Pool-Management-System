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
  Tooltip,
  Badge,
  Row,
  Col,
  Statistic,
} from 'antd';
import {
  ThunderboltOutlined,
  PlusOutlined,
  EditOutlined,
  ReloadOutlined,
  LeftOutlined,
  RightOutlined,
  CalendarOutlined,
  UnorderedListOutlined,
  UserOutlined,
  CheckCircleOutlined,
  ClockCircleOutlined,
  ExperimentOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { waterQualityService } from '../services/waterQualityService';
import AutoAssignPoolDutyModal from './AutoAssignPoolDutyModal';
import UpdatePoolDutyModal from './UpdatePoolDutyModal';

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

function getMonday(d) {
  const date = dayjs(d);
  const day = date.day();
  const diff = day === 0 ? -6 : 1 - day;
  return date.add(diff, 'day').startOf('day');
}

export default function PoolDutyScheduleTab({ onOpenCreateMeasurement }) {
  const [viewMode, setViewMode] = useState('board');
  const [weekStart, setWeekStart] = useState(() => getMonday(dayjs()));
  const [loading, setLoading] = useState(false);
  const [schedules, setSchedules] = useState([]);
  const [options, setOptions] = useState({ staffs: [], shifts: [] });
  const [reloadKey, setReloadKey] = useState(0);

  // Filters
  const [employeeFilter, setEmployeeFilter] = useState(null);
  const [statusFilter, setStatusFilter] = useState(null);

  // Modals
  const [autoAssignOpen, setAutoAssignOpen] = useState(false);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [selectedDuty, setSelectedDuty] = useState(null);

  const weekDates = useMemo(() => {
    return Array.from({ length: 7 }, (_, idx) => weekStart.add(idx, 'day'));
  }, [weekStart]);

  useEffect(() => {
    waterQualityService
      .getDutyScheduleOptions()
      .then((res) => setOptions(res || { staffs: [], shifts: [] }))
      .catch((err) => console.error('Lỗi tải danh sách nhân sự/ca trực bể:', err));
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
  }, [weekStart, employeeFilter, statusFilter, reloadKey]);

  const handleRefresh = () => {
    setLoading(true);
    setReloadKey((k) => k + 1);
  };

  const handleOpenEdit = (dutyItem) => {
    setSelectedDuty(dutyItem);
    setEditModalOpen(true);
  };

  const renderStatusTag = (status) => {
    switch (status) {
      case 'Completed':
        return (
          <Tag color="success" icon={<CheckCircleOutlined />}>
            Hoàn thành
          </Tag>
        );
      case 'InProgress':
        return (
          <Tag color="processing" icon={<ClockCircleOutlined />}>
            Đang trực
          </Tag>
        );
      case 'Scheduled':
        return <Tag color="blue">Đã phân công</Tag>;
      case 'Absent':
        return <Tag color="error">Vắng mặt</Tag>;
      case 'Cancelled':
        return <Tag color="default">Đã hủy</Tag>;
      default:
        return <Tag>{status}</Tag>;
    }
  };

  // Summary metrics for current week
  const totalScheduled = schedules.filter((s) => s.status !== 'Cancelled').length;
  const completedCount = schedules.filter((s) => s.status === 'Completed').length;
  const autoAssignedCount = schedules.filter(
    (s) => s.assignmentType === 'Auto' && s.status !== 'Cancelled'
  ).length;

  const tableColumns = [
    {
      title: 'Ngày trực',
      dataIndex: 'dutyDate',
      key: 'dutyDate',
      width: 130,
      render: (d) => dayjs(d).format('DD/MM/YYYY'),
    },
    {
      title: 'Ca trực bể',
      dataIndex: 'shiftName',
      key: 'shiftName',
      width: 170,
      render: (name, record) => (
        <div>
          <Text strong>{name}</Text>
          <div style={{ fontSize: 12, color: '#8c8c8c' }}>
            {String(record.shiftStartTime).slice(0, 5)} –{' '}
            {String(record.shiftEndTime).slice(0, 5)}
          </div>
        </div>
      ),
    },
    {
      title: 'Nhân viên trực bể',
      dataIndex: 'assignedEmployeeName',
      key: 'assignedEmployeeName',
      width: 200,
      render: (name, record) => (
        <Space>
          <UserOutlined style={{ color: '#1890ff' }} />
          <div>
            <Text strong>{name}</Text>
            <div>
              <Tag size="small" color={record.employeeRole === 'LIFEGUARD' ? 'cyan' : 'geekblue'}>
                {record.employeeRole}
              </Tag>
            </div>
          </div>
        </Space>
      ),
    },
    {
      title: 'Hình thức gán',
      dataIndex: 'assignmentType',
      key: 'assignmentType',
      width: 130,
      align: 'center',
      render: (type) =>
        type === 'Auto' ? (
          <Tag color="gold" icon={<ThunderboltOutlined />}>
            Tự động
          </Tag>
        ) : (
          <Tag color="purple">Cập nhật tay</Tag>
        ),
    },
    {
      title: 'Trạng thái trực',
      dataIndex: 'status',
      key: 'status',
      width: 140,
      align: 'center',
      render: renderStatusTag,
    },
    {
      title: 'Kết quả đo nước trong ca',
      key: 'measurement',
      width: 190,
      render: (_, record) =>
        record.waterQualityLogId ? (
          <Space direction="vertical" size={0}>
            <Text style={{ fontSize: 13 }}>
              pH: <strong>{Number(record.measuredPh).toFixed(2)}</strong> | Clo:{' '}
              <strong>{Number(record.measuredChlorine).toFixed(2)}</strong>
            </Text>
            <Tag
              color={
                record.measurementStatus === 'WithinLimits'
                  ? 'green'
                  : record.measurementStatus === 'Warning'
                    ? 'orange'
                    : 'red'
              }
            >
              {record.measurementStatus === 'WithinLimits'
                ? 'Đạt chuẩn'
                : record.measurementStatus === 'Warning'
                  ? 'Cảnh báo'
                  : 'Nguy hiểm'}
            </Tag>
          </Space>
        ) : (
          <Text type="secondary">Chưa có phiếu đo</Text>
        ),
    },
    {
      title: 'Ghi chú nhiệm vụ',
      dataIndex: 'notes',
      key: 'notes',
      ellipsis: true,
    },
    {
      title: 'Thao tác',
      key: 'actions',
      width: 120,
      align: 'center',
      render: (_, record) => (
        <Button
          type="link"
          icon={<EditOutlined />}
          size="small"
          onClick={() => handleOpenEdit(record)}
        >
          Cập nhật
        </Button>
      ),
    },
  ];

  const shiftsToRender =
    options.shifts && options.shifts.length > 0
      ? options.shifts
      : [
          {
            shiftId: '0f5c2b1a-7d3e-4a6b-8c9d-1e2f3a4b5c6d',
            name: 'Ca sáng',
            startTime: '06:00:00',
            endTime: '11:30:00',
          },
          {
            shiftId: '1a6d3c2b-8e4f-4b7c-9d0e-2f3a4b5c6d7e',
            name: 'Ca chiều',
            startTime: '13:30:00',
            endTime: '19:30:00',
          },
        ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {/* Overview KPI row */}
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={8}>
          <Card size="small">
            <Statistic
              title="Tổng số ca trực bể trong tuần"
              value={totalScheduled}
              suffix={`/ ${shiftsToRender.length * 7} ca`}
              prefix={<CalendarOutlined style={{ color: '#1890ff' }} />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card size="small">
            <Statistic
              title="Ca đã hoàn thành trực & đo nước"
              value={completedCount}
              suffix="ca"
              valueStyle={{ color: '#3f8600' }}
              prefix={<CheckCircleOutlined style={{ color: '#52c41a' }} />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card size="small">
            <Statistic
              title="Ca được gán tự động (Round-Robin)"
              value={autoAssignedCount}
              suffix="ca"
              valueStyle={{ color: '#d48806' }}
              prefix={<ThunderboltOutlined style={{ color: '#faad14' }} />}
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
              placeholder="Trạng thái trực"
              style={{ width: 150 }}
              value={statusFilter}
              onChange={(val) => {
                setLoading(true);
                setStatusFilter(val);
              }}
              options={[
                { value: 'Scheduled', label: 'Đã phân công' },
                { value: 'InProgress', label: 'Đang trực' },
                { value: 'Completed', label: 'Hoàn thành' },
                { value: 'Absent', label: 'Vắng mặt' },
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

            <Button
              icon={<PlusOutlined />}
              onClick={() =>
                handleOpenEdit({
                  dutyDate: dayjs().format('YYYY-MM-DD'),
                  shiftId: shiftsToRender[0]?.shiftId,
                  status: 'Scheduled',
                })
              }
            >
              Gán ca lẻ
            </Button>

            <Button
              type="primary"
              icon={<ThunderboltOutlined />}
              onClick={() => setAutoAssignOpen(true)}
              style={{ background: '#fa8c16', borderColor: '#fa8c16' }}
            >
              Tự động gán lịch trực bể
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
                minWidth: 920,
                border: '1px solid #f0f0f0',
              }}
            >
              <thead>
                <tr style={{ background: '#fafafa' }}>
                  <th
                    style={{
                      padding: '12px',
                      border: '1px solid #f0f0f0',
                      width: 140,
                      textAlign: 'left',
                    }}
                  >
                    Ca trực bể
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
                {shiftsToRender.map((shift) => (
                  <tr key={shift.shiftId}>
                    <td
                      style={{
                        padding: '12px',
                        border: '1px solid #f0f0f0',
                        verticalAlign: 'middle',
                        background: '#fafafa',
                      }}
                    >
                      <Text strong>{shift.name}</Text>
                      <div style={{ fontSize: 12, color: '#8c8c8c' }}>
                        {String(shift.startTime).slice(0, 5)} –{' '}
                        {String(shift.endTime).slice(0, 5)}
                      </div>
                    </td>

                    {weekDates.map((d) => {
                      const dateStr = d.format('YYYY-MM-DD');
                      const slotDuty = schedules.find(
                        (s) =>
                          s.dutyDate === dateStr &&
                          s.shiftId === shift.shiftId &&
                          s.status !== 'Cancelled'
                      );

                      return (
                        <td
                          key={`${shift.shiftId}-${dateStr}`}
                          style={{
                            padding: 8,
                            border: '1px solid #f0f0f0',
                            verticalAlign: 'top',
                            height: 115,
                            background: slotDuty
                              ? slotDuty.status === 'Completed'
                                ? '#f6ffed'
                                : '#ffffff'
                              : '#fcfcfc',
                          }}
                        >
                          {slotDuty ? (
                            <div
                              onClick={() => handleOpenEdit(slotDuty)}
                              style={{
                                cursor: 'pointer',
                                padding: 8,
                                borderRadius: 6,
                                border:
                                  slotDuty.status === 'Completed'
                                    ? '1px solid #b7eb8f'
                                    : '1px solid #91caff',
                                background:
                                  slotDuty.status === 'Completed' ? '#f6ffed' : '#f0f5ff',
                                height: '100%',
                                display: 'flex',
                                flexDirection: 'column',
                                justifyContent: 'space-between',
                              }}
                            >
                              <div>
                                <div
                                  style={{
                                    display: 'flex',
                                    justifyContent: 'space-between',
                                    alignItems: 'center',
                                    gap: 4,
                                  }}
                                >
                                  <Text strong style={{ fontSize: 13 }}>
                                    {slotDuty.assignedEmployeeName}
                                  </Text>
                                  <Tooltip title="Bấm để cập nhật lịch trực bể">
                                    <EditOutlined style={{ fontSize: 12, color: '#1677ff' }} />
                                  </Tooltip>
                                </div>
                                <div style={{ marginTop: 4 }}>
                                  {renderStatusTag(slotDuty.status)}
                                </div>
                              </div>

                              <div style={{ marginTop: 6 }}>
                                {slotDuty.waterQualityLogId ? (
                                  <Tooltip title="Phiếu đo chất lượng nước trong ca">
                                    <Tag
                                      color="cyan"
                                      icon={<ExperimentOutlined />}
                                      style={{ fontSize: 11, margin: 0 }}
                                    >
                                      pH {Number(slotDuty.measuredPh).toFixed(1)} | Clo{' '}
                                      {Number(slotDuty.measuredChlorine).toFixed(1)}
                                    </Tag>
                                  </Tooltip>
                                ) : (
                                  <Badge
                                    status="default"
                                    text={
                                      <span style={{ fontSize: 11, color: '#8c8c8c' }}>
                                        {slotDuty.assignmentType === 'Auto'
                                          ? 'Gán tự động'
                                          : 'Gán thủ công'}
                                      </span>
                                    }
                                  />
                                )}
                              </div>
                            </div>
                          ) : (
                            <div
                              onClick={() =>
                                handleOpenEdit({
                                  dutyDate: dateStr,
                                  shiftId: shift.shiftId,
                                  status: 'Scheduled',
                                })
                              }
                              style={{
                                height: '100%',
                                border: '1px dashed #d9d9d9',
                                borderRadius: 6,
                                display: 'flex',
                                flexDirection: 'column',
                                alignItems: 'center',
                                justifyContent: 'center',
                                cursor: 'pointer',
                                color: '#bfbfbf',
                                padding: 8,
                              }}
                            >
                              <PlusOutlined />
                              <span style={{ fontSize: 12, marginTop: 4 }}>Phân công trực</span>
                            </div>
                          )}
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <Table
            rowKey="id"
            loading={loading}
            dataSource={schedules}
            columns={tableColumns}
            pagination={{ pageSize: 14, showSizeChanger: false }}
            locale={{
              emptyText:
                'Chưa có lịch trực bể trong tuần này. Hãy bấm "Tự động gán lịch trực bể" để phân công nhanh!',
            }}
          />
        )}
      </Card>

      <AutoAssignPoolDutyModal
        visible={autoAssignOpen}
        options={options}
        defaultDateRange={[weekStart, weekStart.add(6, 'day')]}
        onCancel={() => setAutoAssignOpen(false)}
        onSuccess={handleRefresh}
      />

      <UpdatePoolDutyModal
        visible={editModalOpen}
        dutyItem={selectedDuty}
        options={options}
        onCancel={() => setEditModalOpen(false)}
        onSuccess={handleRefresh}
        onOpenCreateMeasurement={onOpenCreateMeasurement}
      />
    </div>
  );
}


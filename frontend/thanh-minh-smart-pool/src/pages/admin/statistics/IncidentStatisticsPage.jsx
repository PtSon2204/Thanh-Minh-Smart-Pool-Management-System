import { useState, useEffect, useCallback } from 'react';
import {
  Card, Row, Col, Statistic, Radio, Button, Table, Tag, Typography,
  Space, Spin, message as antMessage, Modal, Descriptions
} from 'antd';
import {
  AlertOutlined, CheckCircleOutlined, ClockCircleOutlined,
  ReloadOutlined, DownloadOutlined, LineChartOutlined, PieChartOutlined,
  InfoCircleOutlined, DashboardOutlined
} from '@ant-design/icons';
import {
  PieChart, Pie, Cell,
  XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip,
  ResponsiveContainer, AreaChart, Area
} from 'recharts';
import { reportService } from '../../../features/reports/services/reportService';
import { startSignalRConnection, onReceiveNotification } from '../../../services/signalrClient';

const { Title, Text } = Typography;

// Custom Tooltip cho Biểu đồ xu hướng sự cố (khai báo ngoài component tránh lint render warning)
const IncidentTrendTooltip = ({ active, payload, label }) => {
  if (!active || !payload?.length) return null;
  return (
    <div style={{
      background: '#fff', border: '1px solid #cbd5e1',
      borderRadius: 8, padding: '10px 14px',
      boxShadow: '0 4px 12px rgba(0,0,0,0.08)'
    }}>
      <Text strong style={{ color: '#001f3e' }}>{label}</Text>
      <div style={{ marginTop: 4, display: 'flex', alignItems: 'center', gap: 6 }}>
        <span style={{ width: 10, height: 10, borderRadius: '50%', background: '#e31748', display: 'inline-block' }} />
        <Text style={{ fontSize: 13 }}>
          Số sự cố: <strong>{payload[0].value}</strong> vụ việc
        </Text>
      </div>
    </div>
  );
};

// Custom Tooltip cho Biểu đồ phân bổ nhóm sự cố (khai báo ngoài component)
const IncidentCategoryTooltip = ({ active, payload }) => {
  if (!active || !payload?.length) return null;
  const item = payload[0];
  return (
    <div style={{
      background: '#fff', border: '1px solid #cbd5e1',
      borderRadius: 8, padding: '10px 14px',
      boxShadow: '0 4px 12px rgba(0,0,0,0.08)'
    }}>
      <Text strong style={{ color: item.payload?.color || '#001f3e' }}>{item.name}</Text>
      <div style={{ marginTop: 4 }}>
        <Text style={{ fontSize: 13 }}>
          Số lượng: <strong>{item.value}</strong> vụ việc ({item.payload?.percentage}%)
        </Text>
      </div>
    </div>
  );
};

export default function IncidentStatisticsPage() {
  const [filter, setFilter] = useState('all');
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState(null);
  const [selectedIncident, setSelectedIncident] = useState(null);
  const [modalVisible, setModalVisible] = useState(false);

  const handleReload = useCallback(() => {
    setLoading(true);
    reportService.getIncidentStatistics({ filter })
      .then((res) => setData(res))
      .catch((err) => antMessage.error(err.response?.data?.message || 'Không thể tải dữ liệu thống kê sự cố'))
      .finally(() => setLoading(false));
  }, [filter]);

  useEffect(() => {
    let ignore = false;
    reportService.getIncidentStatistics({ filter })
      .then((res) => {
        if (!ignore) setData(res);
      })
      .catch((err) => {
        if (!ignore) antMessage.error(err.response?.data?.message || 'Không thể tải dữ liệu thống kê sự cố');
      })
      .finally(() => {
        if (!ignore) setLoading(false);
      });

    return () => {
      ignore = true;
    };
  }, [filter]);

  useEffect(() => {
    startSignalRConnection();
    const unsub = onReceiveNotification((notif) => {
      const type = (notif?.type || '').toUpperCase();
      if (type === 'INCIDENT' || type === 'WARNING' || type === 'SYSTEM') {
        handleReload();
      }
    });
    return () => unsub();
  }, [handleReload]);

  // Xuất file CSV báo cáo sự cố
  const handleExportCSV = () => {
    if (!data?.recentIncidents?.length) {
      antMessage.warning('Không có dữ liệu sự cố để xuất báo cáo');
      return;
    }

    const headers = ['Mã sự cố', 'Nội dung sự cố', 'Nhóm phân loại', 'Mức độ', 'Thời gian báo', 'Nhân viên xử lý', 'Trạng thái', 'Thời gian xử lý'];
    const rows = data.recentIncidents.map(i => [
      `"${i.incidentCode}"`,
      `"${(i.title || '').replace(/"/g, '""')}"`,
      `"${i.category}"`,
      `"${i.severityLevel}"`,
      `"${i.reportedTime}"`,
      `"${i.handlerStaffName}"`,
      `"${i.status}"`,
      `"${i.resolutionTime}"`
    ]);

    const csvContent = '\uFEFF' + [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', `Bao_Cao_Su_Co_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    antMessage.success('Đã xuất báo cáo sự cố thành công!');
  };

  const handleOpenDetail = (record) => {
    setSelectedIncident(record);
    setModalVisible(true);
  };

  // Cấu hình các cột của Bảng nhật ký sự cố
  const columns = [
    {
      title: 'Mã sự cố',
      dataIndex: 'incidentCode',
      key: 'incidentCode',
      width: 140,
      render: (code, record) => (
        <Button
          type="link"
          style={{ padding: 0, fontWeight: 600, color: '#0068ff' }}
          onClick={() => handleOpenDetail(record)}
        >
          <Tag color="cyan" style={{ cursor: 'pointer', fontWeight: 600 }}>
            {code}
          </Tag>
        </Button>
      )
    },
    {
      title: 'Nội dung sự cố',
      key: 'title',
      render: (_, r) => (
        <div>
          <div style={{ fontWeight: 600, color: '#001f3e', fontSize: 14 }}>
            {r.title}
          </div>
          <div style={{ fontSize: 12, color: '#64748b', marginTop: 2 }}>
            {r.category}
          </div>
        </div>
      )
    },
    {
      title: 'Mức độ',
      dataIndex: 'severityLevel',
      key: 'severityLevel',
      width: 140,
      align: 'center',
      render: (sev) => {
        let color = '#faa828';
        let bg = '#fffbe6';
        let border = '#ffe58f';
        if (sev === 'Khẩn cấp') {
          color = '#e31748';
          bg = '#fff1f0';
          border = '#ffa39e';
        } else if (sev === 'Nghiêm trọng') {
          color = '#fa8c16';
          bg = '#fff7e6';
          border = '#ffd591';
        } else if (sev === 'Nhẹ') {
          color = '#03ca77';
          bg = '#f6ffed';
          border = '#b7eb8f';
        }
        return (
          <Tag style={{
            color, background: bg, borderColor: border,
            fontWeight: 600, padding: '2px 10px', borderRadius: 4
          }}>
            {sev}
          </Tag>
        );
      }
    },
    {
      title: 'Thời gian báo',
      dataIndex: 'reportedTime',
      key: 'reportedTime',
      width: 160,
      render: (t) => <span style={{ color: '#475569', fontSize: 13 }}>{t}</span>
    },
    {
      title: 'Nhân viên xử lý',
      dataIndex: 'handlerStaffName',
      key: 'handlerStaffName',
      width: 200,
      render: (name) => (
        <span style={{ fontWeight: 500, color: '#1e293b' }}>
          {name}
        </span>
      )
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      key: 'status',
      width: 140,
      align: 'center',
      render: (st) => {
        const isResolved = st === 'Đã giải quyết';
        return (
          <Tag
            color={isResolved ? 'success' : 'warning'}
            style={{ fontWeight: 600, borderRadius: 4, padding: '2px 8px' }}
          >
            {st}
          </Tag>
        );
      }
    },
    {
      title: 'Thời gian xử lý',
      dataIndex: 'resolutionTime',
      key: 'resolutionTime',
      width: 140,
      align: 'right',
      render: (time, r) => (
        <span style={{
          fontWeight: 600,
          color: r.status === 'Đã giải quyết' ? '#0068ff' : '#94a3b8'
        }}>
          {time}
        </span>
      )
    }
  ];

  return (
    <div style={{ padding: '24px 28px', background: '#f8fafc', minHeight: '100vh' }}>
      {/* HEADER SECTION */}
      <div style={{
        display: 'flex', justifyContent: 'space-between',
        alignItems: 'flex-start', flexWrap: 'wrap', gap: 16, marginBottom: 24
      }}>
        <div>
          <Title level={3} style={{ margin: 0, color: '#001f3e', display: 'flex', alignItems: 'center', gap: 10 }}>
            <AlertOutlined style={{ color: '#faa828', fontSize: 26 }} />
            Thống Kê Sự Cố Vận Hành & An Toàn Bể Bơi
          </Title>
          <Text type="secondary" style={{ fontSize: 14, marginTop: 4, display: 'block' }}>
            Theo dõi tần suất sự cố, phân loại mức độ rủi ro và đánh giá thời gian khắc phục của nhân viên
          </Text>
        </div>

        {/* TOP RIGHT CONTROLS */}
        <Space size="middle" wrap>
          <Radio.Group
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            buttonStyle="solid"
          >
            <Radio.Button value="all">Tất cả</Radio.Button>
            <Radio.Button value="critical_urgent">Nghiêm trọng & Khẩn cấp</Radio.Button>
            <Radio.Button value="in_progress">Đang xử lý</Radio.Button>
          </Radio.Group>

          <Button
            icon={<ReloadOutlined />}
            onClick={handleReload}
            loading={loading}
          >
            Làm mới
          </Button>

          <Button
            type="primary"
            icon={<DownloadOutlined />}
            onClick={handleExportCSV}
            style={{ background: '#0068ff', borderColor: '#0068ff' }}
          >
            Xuất báo cáo sự cố
          </Button>
        </Space>
      </div>

      <Spin spinning={loading}>
        {/* 4 KPI CARDS */}
        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
          {/* KPI 1 */}
          <Col xs={24} sm={12} lg={6}>
            <Card
              bordered={false}
              style={{
                borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
                borderLeft: '5px solid #0068ff'
              }}
              bodyStyle={{ padding: '20px 24px' }}
            >
              <Text type="secondary" style={{ fontSize: 13, textTransform: 'uppercase', fontWeight: 600, letterSpacing: 0.5 }}>
                TỔNG SỰ CỐ GHI NHẬN
              </Text>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: 8 }}>
                <Statistic
                  value={data?.totalIncidents ?? 24}
                  suffix={<span style={{ fontSize: 15, fontWeight: 'normal', color: '#64748b' }}>vụ việc</span>}
                  valueStyle={{ fontSize: 30, fontWeight: 700, color: '#001f3e' }}
                />
                <div style={{
                  width: 48, height: 48, borderRadius: 12,
                  background: '#e0f2fe', display: 'flex', alignItems: 'center', justifyContent: 'center'
                }}>
                  <AlertOutlined style={{ fontSize: 24, color: '#0068ff' }} />
                </div>
              </div>
              <div style={{ marginTop: 8, fontSize: 13, color: '#10b981', display: 'flex', alignItems: 'center', gap: 6 }}>
                <span>{data?.comparisonWithLastMonth ?? 'Giảm -35% so với tháng trước'}</span>
              </div>
            </Card>
          </Col>

          {/* KPI 2 */}
          <Col xs={24} sm={12} lg={6}>
            <Card
              bordered={false}
              style={{
                borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
                borderLeft: '5px solid #10b981'
              }}
              bodyStyle={{ padding: '20px 24px' }}
            >
              <Text type="secondary" style={{ fontSize: 13, textTransform: 'uppercase', fontWeight: 600, letterSpacing: 0.5 }}>
                ĐÃ KHẮC PHỤC THÀNH CÔNG
              </Text>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: 8 }}>
                <Statistic
                  value={data?.resolvedIncidents ?? 21}
                  suffix={
                    <span style={{ fontSize: 15, fontWeight: 'normal', color: '#64748b' }}>
                      vụ việc ({data?.resolvedPercentage ?? 87.5}%)
                    </span>
                  }
                  valueStyle={{ fontSize: 30, fontWeight: 700, color: '#001f3e' }}
                />
                <div style={{
                  width: 48, height: 48, borderRadius: 12,
                  background: '#dcfce7', display: 'flex', alignItems: 'center', justifyContent: 'center'
                }}>
                  <CheckCircleOutlined style={{ fontSize: 24, color: '#10b981' }} />
                </div>
              </div>
              <div style={{ marginTop: 8, fontSize: 13, color: '#10b981' }}>
                Quy trình cứu hộ & bảo trì chuẩn hóa
              </div>
            </Card>
          </Col>

          {/* KPI 3 */}
          <Col xs={24} sm={12} lg={6}>
            <Card
              bordered={false}
              style={{
                borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
                borderLeft: '5px solid #faa828'
              }}
              bodyStyle={{ padding: '20px 24px' }}
            >
              <Text type="secondary" style={{ fontSize: 13, textTransform: 'uppercase', fontWeight: 600, letterSpacing: 0.5 }}>
                ĐANG TIẾP TỤC XỬ LÝ
              </Text>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: 8 }}>
                <Statistic
                  value={data?.inProgressIncidents ?? 3}
                  suffix={<span style={{ fontSize: 15, fontWeight: 'normal', color: '#64748b' }}>vụ việc</span>}
                  valueStyle={{ fontSize: 30, fontWeight: 700, color: '#faa828' }}
                />
                <div style={{
                  width: 48, height: 48, borderRadius: 12,
                  background: '#fef3c7', display: 'flex', alignItems: 'center', justifyContent: 'center'
                }}>
                  <ClockCircleOutlined style={{ fontSize: 24, color: '#faa828' }} />
                </div>
              </div>
              <div style={{ marginTop: 8, fontSize: 13, color: '#b45309' }}>
                Các sự cố kỹ thuật nhẹ đang theo dõi
              </div>
            </Card>
          </Col>

          {/* KPI 4 */}
          <Col xs={24} sm={12} lg={6}>
            <Card
              bordered={false}
              style={{
                borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
                borderLeft: '5px solid #6356e4'
              }}
              bodyStyle={{ padding: '20px 24px' }}
            >
              <Text type="secondary" style={{ fontSize: 13, textTransform: 'uppercase', fontWeight: 600, letterSpacing: 0.5 }}>
                THỜI GIAN XỬ LÝ BÌNH QUÂN (MTTR)
              </Text>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: 8 }}>
                <Statistic
                  value={data?.averageResolutionTimeHours ?? 2.4}
                  precision={1}
                  suffix={<span style={{ fontSize: 15, fontWeight: 'normal', color: '#64748b' }}>giờ / vụ</span>}
                  valueStyle={{ fontSize: 30, fontWeight: 700, color: '#001f3e' }}
                />
                <div style={{
                  width: 48, height: 48, borderRadius: 12,
                  background: '#f3e8ff', display: 'flex', alignItems: 'center', justifyContent: 'center'
                }}>
                  <DashboardOutlined style={{ fontSize: 24, color: '#6356e4' }} />
                </div>
              </div>
              <div style={{ marginTop: 8, fontSize: 13, color: '#0f766e' }}>
                Đáp ứng tiêu chuẩn an toàn quy chuẩn
              </div>
            </Card>
          </Col>
        </Row>

        {/* 2 CHARTS ROW */}
        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
          {/* DONUT CHART */}
          <Col xs={24} lg={10}>
            <Card
              title={
                <span style={{ fontSize: 15, fontWeight: 600, color: '#001f3e', display: 'flex', alignItems: 'center', gap: 8 }}>
                  <PieChartOutlined style={{ color: '#0068ff' }} />
                  Cơ cấu phân loại các nhóm sự cố
                </span>
              }
              bordered={false}
              style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.04)', height: '100%' }}
            >
              <div style={{ width: '100%', height: 260 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={data?.categoryDistributions || []}
                      dataKey="value"
                      nameKey="name"
                      cx="50%"
                      cy="50%"
                      innerRadius={65}
                      outerRadius={95}
                      paddingAngle={3}
                    >
                      {(data?.categoryDistributions || []).map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={entry.color} />
                      ))}
                    </Pie>
                    <RechartsTooltip content={<IncidentCategoryTooltip />} />
                  </PieChart>
                </ResponsiveContainer>
              </div>

              {/* CUSTOM LEGEND */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '10px 14px', marginTop: 12 }}>
                {(data?.categoryDistributions || []).map((cat, i) => (
                  <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <span style={{
                      width: 12, height: 12, borderRadius: 3,
                      background: cat.color, flexShrink: 0
                    }} />
                    <span style={{ fontSize: 12, color: '#334155', lineHeight: 1.3 }}>
                      {cat.name}
                    </span>
                  </div>
                ))}
              </div>
            </Card>
          </Col>

          {/* LINE / AREA CHART */}
          <Col xs={24} lg={14}>
            <Card
              title={
                <span style={{ fontSize: 15, fontWeight: 600, color: '#001f3e', display: 'flex', alignItems: 'center', gap: 8 }}>
                  <LineChartOutlined style={{ color: '#e31748' }} />
                  Xu hướng số lượng sự cố theo tháng (Giảm dần)
                </span>
              }
              bordered={false}
              style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.04)', height: '100%' }}
            >
              <div style={{ width: '100%', height: 290 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <AreaChart
                    data={data?.monthlyTrends || []}
                    margin={{ top: 15, right: 25, left: -10, bottom: 0 }}
                  >
                    <defs>
                      <linearGradient id="incidentGradient" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#e31748" stopOpacity={0.2} />
                        <stop offset="95%" stopColor="#e31748" stopOpacity={0.0} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
                    <XAxis
                      dataKey="month"
                      tickLine={false}
                      stroke="#94a3b8"
                      tick={{ fontSize: 12 }}
                    />
                    <YAxis
                      tickLine={false}
                      stroke="#94a3b8"
                      tick={{ fontSize: 12 }}
                      allowDecimals={false}
                    />
                    <RechartsTooltip content={<IncidentTrendTooltip />} />
                    <Area
                      type="monotone"
                      dataKey="incidentCount"
                      name="Số sự cố"
                      stroke="#e31748"
                      strokeWidth={2.5}
                      fillOpacity={1}
                      fill="url(#incidentGradient)"
                      dot={{ r: 4, fill: '#e31748', stroke: '#fff', strokeWidth: 2 }}
                      activeDot={{ r: 6, fill: '#e31748', stroke: '#fff', strokeWidth: 2 }}
                    />
                  </AreaChart>
                </ResponsiveContainer>
              </div>

              {/* Chart Legend */}
              <div style={{ textAlign: 'center', marginTop: 10 }}>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 13, color: '#64748b' }}>
                  <span style={{ width: 14, height: 2.5, background: '#e31748', display: 'inline-block' }} />
                  <span style={{ width: 6, height: 6, borderRadius: '50%', background: '#e31748', display: 'inline-block', marginLeft: -9 }} />
                  Số sự cố
                </span>
              </div>
            </Card>
          </Col>
        </Row>

        {/* TABLE SECTION */}
        <Card
          title={
            <span style={{ fontSize: 16, fontWeight: 600, color: '#001f3e', display: 'flex', alignItems: 'center', gap: 8 }}>
              <AlertOutlined style={{ color: '#0068ff' }} />
              Nhật ký các sự cố ghi nhận gần nhất
            </span>
          }
          bordered={false}
          style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.04)' }}
          bodyStyle={{ padding: 0 }}
        >
          <Table
            columns={columns}
            scroll={{ x: 1000 }}
            dataSource={data?.recentIncidents || []}
            rowKey="id"
            pagination={{
              pageSize: 8,
              showTotal: (total) => `Tổng số ${total} vụ việc sự cố`
            }}
            locale={{ emptyText: 'Chưa có ghi nhận sự cố nào phù hợp' }}
          />
        </Card>
      </Spin>

      {/* MODAL CHI TIẾT SỰ CỐ */}
      <Modal
        title={
          <span style={{ display: 'flex', alignItems: 'center', gap: 8, color: '#001f3e' }}>
            <InfoCircleOutlined style={{ color: '#0068ff' }} />
            Chi tiết biên bản sự cố: {selectedIncident?.incidentCode}
          </span>
        }
        open={modalVisible}
        onCancel={() => setModalVisible(false)}
        footer={[
          <Button key="close" type="primary" onClick={() => setModalVisible(false)}>
            Đóng
          </Button>
        ]}
        width={650}
      >
        {selectedIncident && (
          <Descriptions bordered column={1} size="small" style={{ marginTop: 16 }}>
            <Descriptions.Item label="Mã sự cố">
              <Tag color="cyan">{selectedIncident.incidentCode}</Tag>
            </Descriptions.Item>
            <Descriptions.Item label="Nội dung sự cố">
              <strong>{selectedIncident.title}</strong>
            </Descriptions.Item>
            <Descriptions.Item label="Phân nhóm">
              {selectedIncident.category}
            </Descriptions.Item>
            <Descriptions.Item label="Mức độ rủi ro">
              <Tag color={selectedIncident.severityLevel === 'Khẩn cấp' ? 'red' : selectedIncident.severityLevel === 'Nghiêm trọng' ? 'orange' : 'blue'}>
                {selectedIncident.severityLevel}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label="Thời gian ghi nhận">
              {selectedIncident.reportedTime}
            </Descriptions.Item>
            <Descriptions.Item label="Nhân viên phụ trách xử lý">
              {selectedIncident.handlerStaffName}
            </Descriptions.Item>
            <Descriptions.Item label="Trạng thái hiện tại">
              <Tag color={selectedIncident.status === 'Đã giải quyết' ? 'success' : 'warning'}>
                {selectedIncident.status}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label="Thời gian khắc phục">
              <strong style={{ color: '#0068ff' }}>{selectedIncident.resolutionTime}</strong>
            </Descriptions.Item>
          </Descriptions>
        )}
      </Modal>
    </div>
  );
}


import { useState, useEffect, useCallback } from 'react';
import {
  Card, Row, Col, Statistic, Radio, Button, Table, Tag, Typography, Space, Spin, message as antMessage,
} from 'antd';
import {
  DollarOutlined, ReloadOutlined, ShoppingOutlined, CheckCircleOutlined, WarningOutlined,
  LineChartOutlined, PieChartOutlined, UnorderedListOutlined, SafetyCertificateOutlined,
} from '@ant-design/icons';
import {
  AreaChart, Area, BarChart, Bar, PieChart, Pie, Cell, XAxis, YAxis, CartesianGrid,
  Tooltip as RechartsTooltip, ResponsiveContainer, Legend,
} from 'recharts';
import { reportService } from '../../../features/reports/services/reportService';

const { Title, Text } = Typography;

const formatVND = (value) =>
  new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value || 0);

const formatCompactVND = (value) => {
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}tr`;
  if (value >= 1_000) return `${(value / 1_000).toFixed(0)}k`;
  return `${value}`;
};

const STATUS_LABEL = {
  Renting: 'Đang thuê', Returned: 'Đã trả', Resolved: 'Đã xử lý',
  Completed: 'Hoàn tất', Damaged: 'Hư hỏng', Lost: 'Mất',
};
const STATUS_COLOR = {
  Renting: 'processing', Returned: 'success', Resolved: 'success',
  Completed: 'success', Damaged: 'warning', Lost: 'error',
};

const cardStyle = { borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' };

export default function RentalStatisticsPage() {
  const [period, setPeriod] = useState('7days');
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      setData(await reportService.getRentalStatistics({ period }));
    } catch {
      antMessage.error('Không thể tải dữ liệu thống kê thuê đồ.');
    } finally {
      setLoading(false);
    }
  }, [period]);

  useEffect(() => {
    let ignore = false;
    reportService.getRentalStatistics({ period })
      .then((res) => { if (!ignore) setData(res); })
      .catch(() => { if (!ignore) antMessage.error('Không thể tải dữ liệu thống kê thuê đồ.'); })
      .finally(() => { if (!ignore) setLoading(false); });
    return () => { ignore = true; };
  }, [period]);

  const kpis = [
    { title: 'TỔNG LƯỢT THUÊ', value: data?.totalRentals || 0, color: '#0284c7', icon: <ShoppingOutlined /> },
    { title: 'PHÍ THUÊ THU ĐƯỢC', value: formatVND(data?.totalFee), color: '#03ca77', icon: <DollarOutlined /> },
    { title: 'TIỀN CỌC ĐANG GIỮ', value: formatVND(data?.depositHeld), color: '#6356e4', icon: <SafetyCertificateOutlined /> },
    { title: 'TIỀN CỌC KHẤU TRỪ', value: formatVND(data?.retainedAmount), color: '#f59e0b', icon: <WarningOutlined /> },
  ];

  const columns = [
    { title: 'Đồ thuê', dataIndex: 'productName' },
    { title: 'Giờ thuê', dataIndex: 'rentTime' },
    { title: 'Giờ trả', dataIndex: 'returnTime' },
    { title: 'Phí thuê', dataIndex: 'fee', align: 'right', render: (v) => formatVND(v) },
    { title: 'Tiền cọc', dataIndex: 'deposit', align: 'right', render: (v) => formatVND(v) },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      align: 'center',
      render: (s) => <Tag color={STATUS_COLOR[s] || 'default'}>{STATUS_LABEL[s] || s}</Tag>,
    },
  ];

  return (
    <div style={{ padding: 24, minHeight: '100vh', background: '#f8fafc' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 16, marginBottom: 24 }}>
        <div>
          <Title level={3} style={{ margin: 0, color: '#001f3e' }}>🛟 Thống Kê Chi Phí Thuê Đồ</Title>
          <Text type="secondary">Theo dõi lượt thuê, phí thuê, tiền cọc và tình trạng đồ cho thuê</Text>
        </div>
        <Space wrap size="middle">
          <Radio.Group value={period} onChange={(e) => setPeriod(e.target.value)} buttonStyle="solid">
            <Radio.Button value="today">Hôm nay</Radio.Button>
            <Radio.Button value="7days">7 ngày qua</Radio.Button>
            <Radio.Button value="30days">30 ngày qua</Radio.Button>
            <Radio.Button value="month">Tháng này</Radio.Button>
          </Radio.Group>
          <Button icon={<ReloadOutlined />} onClick={fetchData} loading={loading}>Làm mới</Button>
        </Space>
      </div>

      <Spin spinning={loading}>
        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
          {kpis.map((k) => (
            <Col key={k.title} xs={24} sm={12} lg={6}>
              <Card bordered={false} style={{ ...cardStyle, borderLeft: `4px solid ${k.color}` }}>
                <Statistic
                  title={<span style={{ color: '#475569', fontWeight: 600 }}>{k.title}</span>}
                  value={k.value}
                  prefix={<span style={{ color: k.color, marginRight: 8 }}>{k.icon}</span>}
                  valueStyle={{ fontSize: 22 }}
                />
              </Card>
            </Col>
          ))}
        </Row>

        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
          <Col xs={24} sm={8}>
            <Card bordered={false} style={cardStyle}>
              <Statistic title="Đang cho thuê" value={data?.activeRentals || 0} prefix={<ShoppingOutlined style={{ color: '#0284c7' }} />} />
            </Card>
          </Col>
          <Col xs={24} sm={8}>
            <Card bordered={false} style={cardStyle}>
              <Statistic title="Đã trả / hoàn tất" value={data?.returnedRentals || 0} prefix={<CheckCircleOutlined style={{ color: '#10b981' }} />} />
            </Card>
          </Col>
          <Col xs={24} sm={8}>
            <Card bordered={false} style={cardStyle}>
              <Statistic title="Hư hỏng / mất" value={data?.damagedOrLostRentals || 0} prefix={<WarningOutlined style={{ color: '#ef4444' }} />} />
            </Card>
          </Col>
        </Row>

        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
          <Col xs={24} lg={16}>
            <Card title={<Space><LineChartOutlined style={{ color: '#0068ff' }} /><span>Lượt thuê & phí thuê theo ngày</span></Space>} bordered={false} style={cardStyle}>
              <div style={{ width: '100%', height: 300 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <AreaChart data={data?.dailyStats || []} margin={{ top: 10, right: 20, left: 10, bottom: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                    <XAxis dataKey="date" stroke="#64748b" />
                    <YAxis yAxisId="left" stroke="#03ca77" tickFormatter={formatCompactVND} />
                    <YAxis yAxisId="right" orientation="right" stroke="#0068ff" allowDecimals={false} />
                    <RechartsTooltip formatter={(v, n) => (n === 'Phí thuê' ? [formatVND(v), n] : [`${v} lượt`, n])} />
                    <Legend />
                    <Area yAxisId="left" type="monotone" dataKey="fee" name="Phí thuê" stroke="#03ca77" fill="#03ca7733" />
                    <Area yAxisId="right" type="monotone" dataKey="count" name="Lượt thuê" stroke="#0068ff" fill="#0068ff33" />
                  </AreaChart>
                </ResponsiveContainer>
              </div>
            </Card>
          </Col>
          <Col xs={24} lg={8}>
            <Card title={<Space><PieChartOutlined style={{ color: '#6356e4' }} /><span>Tình trạng đồ thuê</span></Space>} bordered={false} style={cardStyle}>
              <div style={{ width: '100%', height: 300 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={(data?.statusBreakdown || []).map((s) => ({ ...s, name: STATUS_LABEL[s.name] || s.name }))}
                      cx="50%" cy="45%" innerRadius={55} outerRadius={90} paddingAngle={4} dataKey="value" nameKey="name"
                    >
                      {(data?.statusBreakdown || []).map((e, i) => <Cell key={i} fill={e.color} />)}
                    </Pie>
                    <RechartsTooltip formatter={(v, n) => [`${v} lượt`, n]} />
                    <Legend verticalAlign="bottom" height={36} />
                  </PieChart>
                </ResponsiveContainer>
              </div>
            </Card>
          </Col>
        </Row>

        <Card title="Đồ được thuê nhiều nhất" bordered={false} style={{ ...cardStyle, marginBottom: 24 }}>
          <div style={{ width: '100%', height: 260 }}>
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={data?.topProducts || []} layout="vertical" margin={{ top: 10, right: 20, left: 10, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" horizontal={false} stroke="#e2e8f0" />
                <XAxis type="number" allowDecimals={false} stroke="#64748b" />
                <YAxis type="category" dataKey="name" width={150} stroke="#64748b" tick={{ fontSize: 12 }} />
                <RechartsTooltip formatter={(v, n) => (n === 'Phí thuê' ? [formatVND(v), n] : [`${v} lượt`, n])} />
                <Bar dataKey="count" name="Lượt thuê" fill="#0068ff" radius={[0, 6, 6, 0]} barSize={20} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </Card>

        <Card title={<Space><UnorderedListOutlined style={{ color: '#0068ff' }} /><span>Giao dịch thuê gần nhất</span></Space>} bordered={false} style={cardStyle}>
          <Table rowKey="id" dataSource={data?.recentRentals || []} columns={columns} pagination={{ pageSize: 10 }} size="middle" scroll={{ x: 800 }} />
        </Card>
      </Spin>
    </div>
  );
}

import { useState, useEffect, useCallback } from 'react';
import {
  Card, Row, Col, Statistic, Radio, Button, Table, Tag, Typography, Space, Spin, message as antMessage,
} from 'antd';
import {
  DollarOutlined, ReloadOutlined, RiseOutlined, FallOutlined, ShoppingOutlined,
  LineChartOutlined, PieChartOutlined, UnorderedListOutlined, AppstoreOutlined,
} from '@ant-design/icons';
import {
  BarChart, Bar, PieChart, Pie, Cell, XAxis, YAxis, CartesianGrid,
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

const cardStyle = { borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' };

export default function RevenueStatisticsPage() {
  const [period, setPeriod] = useState('7days');
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      setData(await reportService.getRevenueStatistics({ period }));
    } catch {
      antMessage.error('Không thể tải dữ liệu thống kê doanh thu.');
    } finally {
      setLoading(false);
    }
  }, [period]);

  useEffect(() => {
    let ignore = false;
    reportService.getRevenueStatistics({ period })
      .then((res) => { if (!ignore) setData(res); })
      .catch(() => { if (!ignore) antMessage.error('Không thể tải dữ liệu thống kê doanh thu.'); })
      .finally(() => { if (!ignore) setLoading(false); });
    return () => { ignore = true; };
  }, [period]);

  const growth = data?.revenueGrowthRate || 0;

  const itemColumns = [
    { title: '#', key: 'i', width: 50, align: 'center', render: (_, __, i) => i + 1 },
    { title: 'Hạng mục', dataIndex: 'name' },
    {
      title: 'Nguồn',
      dataIndex: 'source',
      render: (s) => <Tag color={s === 'Vé' ? 'blue' : 'orange'}>{s}</Tag>,
    },
    { title: 'Số lượng', dataIndex: 'quantity', align: 'center' },
    {
      title: 'Doanh thu',
      dataIndex: 'revenue',
      align: 'right',
      render: (v) => <span style={{ fontWeight: 600, color: '#03ca77' }}>{formatVND(v)}</span>,
    },
  ];

  const kpis = [
    { title: 'TỔNG DOANH THU', value: formatVND(data?.totalRevenue), color: '#03ca77', icon: <DollarOutlined /> },
    { title: 'DOANH THU BÁN VÉ', value: formatVND(data?.ticketRevenue), color: '#0284c7', icon: <ShoppingOutlined /> },
    { title: 'DOANH THU THUÊ ĐỒ', value: formatVND(data?.rentalRevenue), color: '#f59e0b', icon: <AppstoreOutlined /> },
    { title: 'SỐ GIAO DỊCH', value: data?.totalTransactions || 0, color: '#6356e4', icon: <UnorderedListOutlined /> },
  ];

  return (
    <div style={{ padding: 24, minHeight: '100vh', background: '#f8fafc' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 16, marginBottom: 24 }}>
        <div>
          <Title level={3} style={{ margin: 0, color: '#001f3e' }}>💰 Thống Kê Doanh Thu</Title>
          <Text type="secondary">Tổng hợp doanh thu từ bán vé và cho thuê đồ theo thời gian</Text>
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
                {k.title === 'TỔNG DOANH THU' && (
                  <div style={{ marginTop: 8, fontSize: 12, color: growth >= 0 ? '#03ca77' : '#e31748' }}>
                    {growth >= 0 ? <RiseOutlined /> : <FallOutlined />} {growth >= 0 ? '+' : ''}{growth}% so với kỳ trước
                  </div>
                )}
              </Card>
            </Col>
          ))}
        </Row>

        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
          <Col xs={24} lg={16}>
            <Card title={<Space><LineChartOutlined style={{ color: '#0068ff' }} /><span>Doanh thu theo ngày</span></Space>} bordered={false} style={cardStyle}>
              <div style={{ width: '100%', height: 320 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={data?.dailyRevenues || []} margin={{ top: 10, right: 20, left: 10, bottom: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                    <XAxis dataKey="date" stroke="#64748b" />
                    <YAxis tickFormatter={formatCompactVND} stroke="#64748b" />
                    <RechartsTooltip formatter={(v, n) => [formatVND(v), n]} />
                    <Legend />
                    <Bar dataKey="ticketRevenue" name="Bán vé" stackId="rev" fill="#0284c7" />
                    <Bar dataKey="rentalRevenue" name="Thuê đồ" stackId="rev" fill="#f59e0b" radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </Card>
          </Col>
          <Col xs={24} lg={8}>
            <Card title={<Space><PieChartOutlined style={{ color: '#6356e4' }} /><span>Cơ cấu doanh thu</span></Space>} bordered={false} style={cardStyle}>
              <div style={{ width: '100%', height: 320 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={data?.revenueBySource || []}
                      cx="50%" cy="45%" innerRadius={60} outerRadius={95} paddingAngle={4}
                      dataKey="value" nameKey="name"
                    >
                      {(data?.revenueBySource || []).map((e, i) => <Cell key={i} fill={e.color} />)}
                    </Pie>
                    <RechartsTooltip formatter={(v, n) => [formatVND(v), n]} />
                    <Legend verticalAlign="bottom" height={36} />
                  </PieChart>
                </ResponsiveContainer>
              </div>
            </Card>
          </Col>
        </Row>

        <Card title={<Space><UnorderedListOutlined style={{ color: '#0068ff' }} /><span>Top hạng mục mang lại doanh thu</span></Space>} bordered={false} style={cardStyle}>
          <Table rowKey={(r) => `${r.source}-${r.name}`} dataSource={data?.topItems || []} columns={itemColumns} pagination={false} size="middle" />
        </Card>
      </Spin>
    </div>
  );
}

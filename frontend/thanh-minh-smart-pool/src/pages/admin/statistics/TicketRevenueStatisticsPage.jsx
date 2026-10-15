import { useState, useEffect, useCallback } from 'react';
import {
  Card, Row, Col, Statistic, Radio, Button, Table, Tag, Typography,
  Space, Spin, message as antMessage, Tooltip
} from 'antd';
import {
  ShoppingCartOutlined, DollarOutlined, BarChartOutlined,
  ReloadOutlined, RiseOutlined, FallOutlined, TagOutlined,
  PieChartOutlined, LineChartOutlined, UnorderedListOutlined,
  CreditCardOutlined
} from '@ant-design/icons';
import {
  AreaChart, Area, BarChart, Bar, PieChart, Pie, Cell,
  XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip,
  ResponsiveContainer, Legend
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

export default function TicketRevenueStatisticsPage() {
  const [period, setPeriod] = useState('7days');
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const res = await reportService.getTicketRevenueStatistics({ period });
      setData(res);
    } catch {
      antMessage.error('Không thể tải dữ liệu thống kê bán vé.');
    } finally {
      setLoading(false);
    }
  }, [period]);

  useEffect(() => {
    let ignore = false;
    setLoading(true);
    reportService.getTicketRevenueStatistics({ period })
      .then((res) => {
        if (!ignore) setData(res);
      })
      .catch(() => {
        if (!ignore) antMessage.error('Không thể tải dữ liệu thống kê bán vé.');
      })
      .finally(() => {
        if (!ignore) setLoading(false);
      });

    return () => {
      ignore = true;
    };
  }, [period]);

  // ============ COLUMNS cho bảng giao dịch ============
  const transactionColumns = [
    {
      title: '#',
      key: 'index',
      width: 50,
      align: 'center',
      render: (_, __, index) => (
        <span style={{ color: '#64748b', fontWeight: 600 }}>{index + 1}</span>
      ),
    },
    {
      title: 'Khách hàng',
      dataIndex: 'customerName',
      render: (name, record) => (
        <div>
          <Text strong style={{ color: '#0f172a' }}>{name}</Text>
          <br />
          <Text type="secondary" style={{ fontSize: 12 }}>{record.phone || '—'}</Text>
        </div>
      ),
    },
    {
      title: 'Loại vé',
      dataIndex: 'ticketType',
      render: (type) => {
        let tagColor = 'blue';
        if (type?.includes('tháng')) {
          tagColor = type.includes('trẻ em') ? 'magenta' : 'purple';
        } else if (type?.includes('trẻ em')) {
          tagColor = 'cyan';
        }
        return <Tag color={tagColor}>{type || '—'}</Tag>;
      },
    },
    {
      title: 'SL',
      dataIndex: 'quantity',
      align: 'center',
      width: 60,
      render: (qty) => (
        <span style={{ fontWeight: 700, color: '#0284c7' }}>{qty}</span>
      ),
    },
    {
      title: 'Thành tiền',
      dataIndex: 'totalAmount',
      align: 'right',
      render: (amount) => (
        <span style={{ fontWeight: 600, color: '#16a34a' }}>
          {formatVND(amount)}
        </span>
      ),
    },
    {
      title: 'Thanh toán',
      dataIndex: 'paymentMethod',
      align: 'center',
      render: (method) => {
        const color = method === 'Tiền mặt' ? 'green' :
                      method === 'Chuyển khoản' ? 'blue' : 'default';
        return <Tag color={color}>{method || '—'}</Tag>;
      },
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      align: 'center',
      render: (status) => {
        const colorMap = {
          'Completed': 'success',
          'Pending': 'warning',
          'Cancelled': 'error',
        };
        const labelMap = {
          'Completed': 'Hoàn thành',
          'Pending': 'Chờ xử lý',
          'Cancelled': 'Đã hủy',
        };
        return <Tag color={colorMap[status] || 'default'}>{labelMap[status] || status}</Tag>;
      },
    },
    {
      title: 'Thời gian',
      dataIndex: 'createdAt',
      render: (time) => <Text type="secondary" style={{ fontSize: 12 }}>{time}</Text>,
    },
  ];

  // Custom Tooltip cho biểu đồ doanh thu
  const RevenueChartTooltip = ({ active, payload, label }) => {
    if (!active || !payload?.length) return null;
    return (
      <div style={{
        background: '#fff', border: '1px solid #cbd5e1',
        borderRadius: 8, padding: '10px 14px',
        boxShadow: '0 4px 12px rgba(0,0,0,0.08)'
      }}>
        <Text strong style={{ color: '#0f172a' }}>{label}</Text>
        {payload.map((p, i) => (
          <div key={i} style={{ marginTop: 4, display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{ width: 10, height: 10, borderRadius: '50%', background: p.color, display: 'inline-block' }} />
            <Text style={{ fontSize: 13 }}>
              {p.name}: {p.name.includes('Doanh thu') ? formatVND(p.value) : `${p.value} vé`}
            </Text>
          </div>
        ))}
      </div>
    );
  };

  return (
    <div style={{ padding: 24, minHeight: '100vh', background: '#f8fafc' }}>
      {/* Header & Bộ lọc thời gian */}
      <div style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: 16,
        marginBottom: 24
      }}>
        <div>
          <Title level={3} style={{ margin: 0, color: '#0f172a' }}>
            🎫 Thống Kê Bán Vé & Doanh Thu
          </Title>
          <Text type="secondary" style={{ fontSize: 14 }}>
            Theo dõi số lượng vé bán ra, doanh thu theo loại vé và thời gian
          </Text>
        </div>

        <Space wrap>
          <Radio.Group
            value={period}
            onChange={(e) => setPeriod(e.target.value)}
            buttonStyle="solid"
          >
            <Radio.Button value="today">Hôm nay</Radio.Button>
            <Radio.Button value="7days">7 ngày qua</Radio.Button>
            <Radio.Button value="30days">30 ngày qua</Radio.Button>
            <Radio.Button value="month">Tháng này</Radio.Button>
          </Radio.Group>

          <Button
            icon={<ReloadOutlined />}
            onClick={fetchData}
            loading={loading}
          >
            Làm mới
          </Button>
        </Space>
      </div>

      <Spin spinning={loading}>
        {/* 4 Thẻ KPI chính */}
        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
          <Col xs={24} sm={12} lg={6}>
            <Card
              bordered={false}
              style={{
                borderRadius: 12,
                boxShadow: '0 2px 10px rgba(0,0,0,0.04)',
                background: 'linear-gradient(135deg, #ffffff 0%, #f0f9ff 100%)',
                borderLeft: '4px solid #0284c7'
              }}
            >
              <Statistic
                title={<span style={{ color: '#475569', fontWeight: 600 }}>TỔNG VÉ BÁN RA</span>}
                value={data?.totalTicketsSold || 0}
                prefix={<ShoppingCartOutlined style={{ color: '#0284c7', marginRight: 8 }} />}
                suffix={<span style={{ fontSize: 13, color: '#64748b' }}>vé</span>}
              />
              <div style={{ marginTop: 8, fontSize: 12, color: '#64748b' }}>
                Tổng số vé đã bán trong kỳ
              </div>
            </Card>
          </Col>

          <Col xs={24} sm={12} lg={6}>
            <Card
              bordered={false}
              style={{
                borderRadius: 12,
                boxShadow: '0 2px 10px rgba(0,0,0,0.04)',
                background: 'linear-gradient(135deg, #ffffff 0%, #f0fdf4 100%)',
                borderLeft: '4px solid #16a34a'
              }}
            >
              <Statistic
                title={<span style={{ color: '#475569', fontWeight: 600 }}>TỔNG DOANH THU</span>}
                value={data?.totalRevenue || 0}
                prefix={<DollarOutlined style={{ color: '#16a34a', marginRight: 8 }} />}
                formatter={(val) => formatVND(val)}
              />
              <div style={{
                marginTop: 8, fontSize: 12,
                color: (data?.revenueGrowthRate || 0) >= 0 ? '#16a34a' : '#ef4444',
                display: 'flex', alignItems: 'center', gap: 4
              }}>
                {(data?.revenueGrowthRate || 0) >= 0
                  ? <RiseOutlined />
                  : <FallOutlined />}
                <span>{(data?.revenueGrowthRate || 0) >= 0 ? '+' : ''}{data?.revenueGrowthRate || 0}% so với kỳ trước</span>
              </div>
            </Card>
          </Col>

          <Col xs={24} sm={12} lg={6}>
            <Card
              bordered={false}
              style={{
                borderRadius: 12,
                boxShadow: '0 2px 10px rgba(0,0,0,0.04)',
                background: 'linear-gradient(135deg, #ffffff 0%, #faf5ff 100%)',
                borderLeft: '4px solid #8b5cf6'
              }}
            >
              <Statistic
                title={<span style={{ color: '#475569', fontWeight: 600 }}>GIÁ TRỊ TRUNG BÌNH</span>}
                value={data?.averageOrderValue || 0}
                prefix={<TagOutlined style={{ color: '#8b5cf6', marginRight: 8 }} />}
                formatter={(val) => formatVND(val)}
              />
              <div style={{ marginTop: 8, fontSize: 12, color: '#64748b' }}>
                Giá trị trung bình mỗi đơn hàng
              </div>
            </Card>
          </Col>

          <Col xs={24} sm={12} lg={6}>
            <Card
              bordered={false}
              style={{
                borderRadius: 12,
                boxShadow: '0 2px 10px rgba(0,0,0,0.04)',
                background: 'linear-gradient(135deg, #ffffff 0%, #fff7ed 100%)',
                borderLeft: '4px solid #f59e0b'
              }}
            >
              <Statistic
                title={<span style={{ color: '#475569', fontWeight: 600 }}>LOẠI VÉ BÁN CHẠY</span>}
                value={(data?.ticketTypeSales || [])[0]?.name || '—'}
                prefix={<BarChartOutlined style={{ color: '#f59e0b', marginRight: 8 }} />}
                valueStyle={{ fontSize: 18 }}
              />
              <div style={{ marginTop: 8, fontSize: 12, color: '#64748b' }}>
                {(data?.ticketTypeSales || [])[0]
                  ? `${(data?.ticketTypeSales || [])[0].value} vé đã bán`
                  : 'Chưa có dữ liệu'}
              </div>
            </Card>
          </Col>
        </Row>

        {/* Khối Biểu Đồ 1: Doanh thu theo ngày & Tỷ lệ loại vé */}
        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
          {/* Biểu đồ diện tích doanh thu theo ngày */}
          <Col xs={24} lg={16}>
            <Card
              title={
                <Space>
                  <LineChartOutlined style={{ color: '#0284c7' }} />
                  <span>Doanh thu & Số vé bán theo ngày</span>
                </Space>
              }
              bordered={false}
              style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}
            >
              <div style={{ width: '100%', height: 320 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <AreaChart
                    data={data?.dailyRevenues || []}
                    margin={{ top: 10, right: 20, left: 10, bottom: 0 }}
                  >
                    <defs>
                      <linearGradient id="colorRevenue" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#16a34a" stopOpacity={0.8} />
                        <stop offset="95%" stopColor="#16a34a" stopOpacity={0.05} />
                      </linearGradient>
                      <linearGradient id="colorTicketCount" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#0284c7" stopOpacity={0.8} />
                        <stop offset="95%" stopColor="#0284c7" stopOpacity={0.05} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                    <XAxis dataKey="date" stroke="#64748b" />
                    <YAxis
                      yAxisId="left"
                      stroke="#16a34a"
                      tickFormatter={formatCompactVND}
                    />
                    <YAxis
                      yAxisId="right"
                      orientation="right"
                      stroke="#0284c7"
                    />
                    <RechartsTooltip content={<RevenueChartTooltip />} />
                    <Legend />
                    <Area
                      yAxisId="left"
                      type="monotone"
                      dataKey="revenue"
                      name="Doanh thu"
                      stroke="#16a34a"
                      strokeWidth={2}
                      fillOpacity={1}
                      fill="url(#colorRevenue)"
                    />
                    <Area
                      yAxisId="right"
                      type="monotone"
                      dataKey="ticketCount"
                      name="Số vé bán"
                      stroke="#0284c7"
                      strokeWidth={2}
                      fillOpacity={1}
                      fill="url(#colorTicketCount)"
                    />
                  </AreaChart>
                </ResponsiveContainer>
              </div>
            </Card>
          </Col>

          {/* Biểu đồ tròn tỷ lệ loại vé */}
          <Col xs={24} lg={8}>
            <Card
              title={
                <Space>
                  <PieChartOutlined style={{ color: '#8b5cf6' }} />
                  <span>Tỷ lệ bán theo loại vé</span>
                </Space>
              }
              bordered={false}
              style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}
            >
              <div style={{ width: '100%', height: 320 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={data?.ticketTypeSales || []}
                      cx="50%"
                      cy="45%"
                      innerRadius={60}
                      outerRadius={95}
                      paddingAngle={4}
                      dataKey="value"
                      nameKey="name"
                      label={({ percent }) => `${(percent * 100).toFixed(0)}%`}
                    >
                      {(data?.ticketTypeSales || []).map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={entry.color} />
                      ))}
                    </Pie>
                    <RechartsTooltip
                      formatter={(value, name) => [`${value} vé`, name]}
                    />
                    <Legend verticalAlign="bottom" height={36} />
                  </PieChart>
                </ResponsiveContainer>
              </div>
            </Card>
          </Col>
        </Row>

        {/* Khối Biểu Đồ 2: Doanh thu theo loại vé (Bar chart) */}
        <Card
          title={
            <Space>
              <BarChartOutlined style={{ color: '#005f8e' }} />
              <span>Doanh thu theo loại vé</span>
            </Space>
          }
          bordered={false}
          style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)', marginBottom: 24 }}
        >
          <div style={{ marginBottom: 12, color: '#64748b', fontSize: 13 }}>
            💡 <strong>Phân tích:</strong> So sánh doanh thu giữa vé người lớn và vé trẻ em theo từng hình thức (lượt / tháng).
          </div>
          <div style={{ width: '100%', height: 280 }}>
            <ResponsiveContainer width="100%" height="100%">
              <BarChart
                data={data?.ticketTypeRevenues || []}
                margin={{ top: 10, right: 20, left: 10, bottom: 0 }}
                layout="vertical"
              >
                <CartesianGrid strokeDasharray="3 3" horizontal={false} stroke="#e2e8f0" />
                <XAxis type="number" tickFormatter={formatCompactVND} stroke="#64748b" />
                <YAxis
                  type="category"
                  dataKey="name"
                  width={150}
                  stroke="#64748b"
                  tick={{ fontSize: 12 }}
                />
                <RechartsTooltip
                  formatter={(value, name) => {
                    if (name === 'Doanh thu') return [formatVND(value), name];
                    return [`${value} vé`, name];
                  }}
                  contentStyle={{
                    backgroundColor: '#ffffff',
                    border: '1px solid #cbd5e1',
                    borderRadius: 8
                  }}
                />
                <Bar
                  dataKey="revenue"
                  name="Doanh thu"
                  radius={[0, 6, 6, 0]}
                  barSize={24}
                >
                  {(data?.ticketTypeRevenues || []).map((entry, index) => {
                    const colorMap = {
                      'Vé lượt người lớn': '#0284c7',
                      'Vé lượt trẻ em': '#06b6d4',
                      'Vé tháng người lớn': '#8b5cf6',
                      'Vé tháng trẻ em': '#ec4899',
                    };
                    return <Cell key={`bar-${index}`} fill={colorMap[entry.name] || '#005f8e'} />;
                  })}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        </Card>

        {/* Khối Bảng: Giao dịch gần nhất */}
        <Card
          title={
            <Space>
              <UnorderedListOutlined style={{ color: '#0284c7' }} />
              <span>Giao dịch bán vé gần nhất</span>
            </Space>
          }
          bordered={false}
          style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}
        >
          <Table
            rowKey="id"
            dataSource={data?.recentTransactions || []}
            columns={transactionColumns}
            pagination={{ pageSize: 10 }}
            size="middle"
            scroll={{ x: 900 }}
          />
        </Card>
      </Spin>
    </div>
  );
}

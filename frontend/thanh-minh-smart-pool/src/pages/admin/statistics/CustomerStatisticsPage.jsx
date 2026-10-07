import { useState, useEffect, useCallback } from 'react';
import {
  Card, Row, Col, Statistic, Radio, Button, Table, Tag, Typography,
  Space, Spin, message as antMessage
} from 'antd';
import {
  UserOutlined, UserAddOutlined, IdcardOutlined, CheckCircleOutlined,
  ReloadOutlined, RiseOutlined, TrophyOutlined, ClockCircleOutlined,
  PieChartOutlined, LineChartOutlined
} from '@ant-design/icons';
import {
  AreaChart, Area, BarChart, Bar, PieChart, Pie, Cell,
  XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip,
  ResponsiveContainer, Legend
} from 'recharts';
import { reportService } from '../../../features/reports/services/reportService';

const { Title, Text } = Typography;

export default function CustomerStatisticsPage() {
  const [period, setPeriod] = useState('7days');
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const res = await reportService.getCustomerStatistics({ period });
      setData(res);
    } catch {
      antMessage.error('Không thể tải dữ liệu thống kê khách hàng.');
    } finally {
      setLoading(false);
    }
  }, [period]);

  useEffect(() => {
    let ignore = false;
    reportService.getCustomerStatistics({ period })
      .then((res) => {
        if (!ignore) setData(res);
      })
      .catch(() => {
        if (!ignore) antMessage.error('Không thể tải dữ liệu thống kê khách hàng.');
      })
      .finally(() => {
        if (!ignore) setLoading(false);
      });

    return () => {
      ignore = true;
    };
  }, [period]);

  const topColumns = [
    {
      title: 'Hạng',
      key: 'rank',
      width: 70,
      align: 'center',
      render: (_, __, index) => {
        const colors = ['#f59e0b', '#94a3b8', '#b45309'];
        return index < 3 ? (
          <span style={{
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 28,
            height: 28,
            borderRadius: '50%',
            background: colors[index],
            color: '#fff',
            fontWeight: 'bold',
            fontSize: 13
          }}>
            {index + 1}
          </span>
        ) : (
          <span style={{ color: '#64748b', fontWeight: 600 }}>#{index + 1}</span>
        );
      },
    },
    {
      title: 'Khách hàng',
      dataIndex: 'fullName',
      render: (name, record) => (
        <div>
          <Text strong style={{ color: '#0f172a' }}>{name}</Text>
          <br />
          <Text type="secondary" style={{ fontSize: 12 }}>{record.email || '—'}</Text>
        </div>
      ),
    },
    {
      title: 'Số điện thoại',
      dataIndex: 'phone',
      render: (phone) => phone || '—',
    },
    {
      title: 'Loại thẻ sở hữu',
      dataIndex: 'ticketType',
      render: (type) => {
        const isVip = type.includes('VIP') || type.includes('tháng');
        return <Tag color={isVip ? 'purple' : 'blue'}>{type}</Tag>;
      },
    },
    {
      title: 'Lượt bơi',
      dataIndex: 'totalEntries',
      align: 'center',
      render: (entries) => (
        <span style={{ fontWeight: 700, color: '#0284c7' }}>
          {entries} lượt
        </span>
      ),
    },
    {
      title: 'Tổng chi tiêu',
      dataIndex: 'totalSpending',
      align: 'right',
      render: (spent) => (
        <span style={{ fontWeight: 600, color: '#16a34a' }}>
          {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(spent || 0)}
        </span>
      ),
    },
    {
      title: 'Lần bơi gần nhất',
      dataIndex: 'lastVisit',
      render: (visit) => <Text type="secondary">{visit}</Text>,
    },
  ];

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
            📊 Thống Kê Khách Hàng & Hoạt Động Bể Bơi
          </Title>
          <Text type="secondary" style={{ fontSize: 14 }}>
            Theo dõi lưu lượng khách bơi, tốc độ phát triển hội viên và giờ cao điểm
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
                title={<span style={{ color: '#475569', fontWeight: 600 }}>TỔNG KHÁCH HÀNG</span>}
                value={data?.totalCustomers || 0}
                prefix={<UserOutlined style={{ color: '#0284c7', marginRight: 8 }} />}
                suffix={<span style={{ fontSize: 13, color: '#64748b' }}>người</span>}
              />
              <div style={{ marginTop: 8, fontSize: 12, color: '#16a34a', display: 'flex', alignItems: 'center', gap: 4 }}>
                <RiseOutlined /> <span>Tăng trưởng +{data?.growthRate || 0}% so với kỳ trước</span>
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
                title={<span style={{ color: '#475569', fontWeight: 600 }}>KHÁCH MỚI TRONG KỲ</span>}
                value={data?.newCustomersInPeriod || 0}
                prefix={<UserAddOutlined style={{ color: '#16a34a', marginRight: 8 }} />}
                suffix={<span style={{ fontSize: 13, color: '#64748b' }}>người</span>}
              />
              <div style={{ marginTop: 8, fontSize: 12, color: '#64748b' }}>
                Đăng ký tài khoản mới trong giai đoạn chọn
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
                title={<span style={{ color: '#475569', fontWeight: 600 }}>HỘI VIÊN VÉ THÁNG</span>}
                value={data?.activeMonthlySubscribers || 0}
                prefix={<IdcardOutlined style={{ color: '#8b5cf6', marginRight: 8 }} />}
                suffix={<span style={{ fontSize: 13, color: '#64748b' }}>thẻ đang hoạt động</span>}
              />
              <div style={{ marginTop: 8, fontSize: 12, color: '#64748b' }}>
                Hội viên định kỳ thường xuyên
              </div>
            </Card>
          </Col>

          <Col xs={24} sm={12} lg={6}>
            <Card
              bordered={false}
              style={{
                borderRadius: 12,
                boxShadow: '0 2px 10px rgba(0,0,0,0.04)',
                background: 'linear-gradient(135deg, #ffffff 0%, #eff6ff 100%)',
                borderLeft: '4px solid #005f8e'
              }}
            >
              <Statistic
                title={<span style={{ color: '#475569', fontWeight: 600 }}>TỔNG LƯỢT BƠI (CHECK-IN)</span>}
                value={data?.totalSwimEntries || 0}
                prefix={<CheckCircleOutlined style={{ color: '#005f8e', marginRight: 8 }} />}
                suffix={<span style={{ fontSize: 13, color: '#64748b' }}>lượt vào bể</span>}
              />
              <div style={{ marginTop: 8, fontSize: 12, color: '#64748b' }}>
                Quét mã QR qua cổng thành công
              </div>
            </Card>
          </Col>
        </Row>

        {/* Khối Biểu Đồ 1: Xu hướng bơi & Phân khúc khách */}
        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
          {/* Biểu đồ diện tích xu hướng */}
          <Col xs={24} lg={16}>
            <Card
              title={
                <Space>
                  <LineChartOutlined style={{ color: '#0284c7' }} />
                  <span>Xu hướng lượt khách vào bơi theo thời gian</span>
                </Space>
              }
              bordered={false}
              style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}
            >
              <div style={{ width: '100%', height: 320 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <AreaChart
                    data={data?.dailyTrends || []}
                    margin={{ top: 10, right: 20, left: 0, bottom: 0 }}
                  >
                    <defs>
                      <linearGradient id="colorSwim" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#0284c7" stopOpacity={0.8} />
                        <stop offset="95%" stopColor="#0284c7" stopOpacity={0.05} />
                      </linearGradient>
                      <linearGradient id="colorUser" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#10b981" stopOpacity={0.8} />
                        <stop offset="95%" stopColor="#10b981" stopOpacity={0.05} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                    <XAxis dataKey="date" stroke="#64748b" />
                    <YAxis stroke="#64748b" />
                    <RechartsTooltip
                      contentStyle={{
                        backgroundColor: '#ffffff',
                        border: '1px solid #cbd5e1',
                        borderRadius: 8,
                        boxShadow: '0 4px 12px rgba(0,0,0,0.08)'
                      }}
                    />
                    <Legend />
                    <Area
                      type="monotone"
                      dataKey="swimEntries"
                      name="Lượt khách bơi"
                      stroke="#0284c7"
                      strokeWidth={2}
                      fillOpacity={1}
                      fill="url(#colorSwim)"
                    />
                    <Area
                      type="monotone"
                      dataKey="newUsers"
                      name="Khách đăng ký mới"
                      stroke="#10b981"
                      strokeWidth={2}
                      fillOpacity={1}
                      fill="url(#colorUser)"
                    />
                  </AreaChart>
                </ResponsiveContainer>
              </div>
            </Card>
          </Col>

          {/* Biểu đồ phân khúc khách hàng */}
          <Col xs={24} lg={8}>
            <Card
              title={
                <Space>
                  <PieChartOutlined style={{ color: '#8b5cf6' }} />
                  <span>Cơ cấu phân khúc khách hàng</span>
                </Space>
              }
              bordered={false}
              style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}
            >
              <div style={{ width: '100%', height: 320 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={data?.segments || []}
                      cx="50%"
                      cy="45%"
                      innerRadius={60}
                      outerRadius={95}
                      paddingAngle={4}
                      dataKey="value"
                      nameKey="name"
                      label={({ name, percent }) => `${name.split(' ')[0]} ${(percent * 100).toFixed(0)}%`}
                    >
                      {(data?.segments || []).map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={entry.color} />
                      ))}
                    </Pie>
                    <RechartsTooltip />
                    <Legend verticalAlign="bottom" height={36} />
                  </PieChart>
                </ResponsiveContainer>
              </div>
            </Card>
          </Col>
        </Row>

        {/* Khối Biểu Đồ 2: Giờ cao điểm trong ngày */}
        <Card
          title={
            <Space>
              <ClockCircleOutlined style={{ color: '#005f8e' }} />
              <span>Mật độ khách bơi theo khung giờ trong ngày (Khung giờ cao điểm)</span>
            </Space>
          }
          bordered={false}
          style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)', marginBottom: 24 }}
        >
          <div style={{ marginBottom: 12, color: '#64748b', fontSize: 13 }}>
            💡 <strong>Gợi ý quản lý:</strong> Biểu đồ giúp xác định khung giờ đông nhất (thường là 06:00 - 08:00 và 17:00 - 19:00) để phân công thêm nhân viên cứu hộ và chuẩn bị tủ đồ.
          </div>
          <div style={{ width: '100%', height: 260 }}>
            <ResponsiveContainer width="100%" height="100%">
              <BarChart
                data={data?.hourlyDistribution || []}
                margin={{ top: 10, right: 10, left: -10, bottom: 0 }}
              >
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                <XAxis dataKey="hour" stroke="#64748b" />
                <YAxis stroke="#64748b" />
                <RechartsTooltip
                  formatter={(value) => [`${value} lượt vào bơi`, 'Số lượng khách']}
                  contentStyle={{
                    backgroundColor: '#ffffff',
                    border: '1px solid #cbd5e1',
                    borderRadius: 8
                  }}
                />
                <Bar
                  dataKey="count"
                  name="Số lượt khách vào bơi"
                  fill="#005f8e"
                  radius={[4, 4, 0, 0]}
                />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </Card>

        {/* Khối Bảng: Top khách hàng tiêu biểu */}
        <Card
          title={
            <Space>
              <TrophyOutlined style={{ color: '#f59e0b' }} />
              <span>Top khách hàng tích cực và thân thiết nhất</span>
            </Space>
          }
          bordered={false}
          style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}
        >
          <Table
            rowKey="id"
            dataSource={data?.topCustomers || []}
            columns={topColumns}
            pagination={false}
            size="middle"
          />
        </Card>
      </Spin>
    </div>
  );
}

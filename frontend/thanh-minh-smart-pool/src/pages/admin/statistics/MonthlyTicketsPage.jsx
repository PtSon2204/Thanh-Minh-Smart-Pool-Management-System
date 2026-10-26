import { useState, useEffect, useCallback } from 'react';
import {
  Card, Row, Col, Statistic, Radio, Button, Table, Tag, Typography,
  Space, Spin, message as antMessage, Modal
} from 'antd';
import {
  IdcardOutlined, CheckCircleOutlined, AlertOutlined, SyncOutlined,
  ReloadOutlined, DownloadOutlined, MailOutlined,
  PieChartOutlined, BarChartOutlined
} from '@ant-design/icons';
import {
  BarChart, Bar, PieChart, Pie, Cell,
  XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip,
  ResponsiveContainer, Legend
} from 'recharts';
import { reportService } from '../../../features/reports/services/reportService';

const { Title, Text } = Typography;

// Custom Tooltip cho Biểu đồ xu hướng (khai báo ngoài component)
const MonthlyTrendTooltip = ({ active, payload, label }) => {
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
            {p.name}: <strong>{p.value}</strong> hội viên
          </Text>
        </div>
      ))}
    </div>
  );
};

export default function MonthlyTicketsPage() {
  const [filter, setFilter] = useState('expiring_soon');
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState(null);
  const [sendingEmailId, setSendingEmailId] = useState(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const res = await reportService.getMonthlyTicketStatistics({ filter });
      setData(res);
    } catch {
      antMessage.error('Không thể tải dữ liệu thống kê vé tháng.');
    } finally {
      setLoading(false);
    }
  }, [filter]);

  useEffect(() => {
    let ignore = false;
    reportService.getMonthlyTicketStatistics({ filter })
      .then((res) => {
        if (!ignore) setData(res);
      })
      .catch(() => {
        if (!ignore) antMessage.error('Không thể tải dữ liệu thống kê vé tháng.');
      })
      .finally(() => {
        if (!ignore) setLoading(false);
      });

    return () => {
      ignore = true;
    };
  }, [filter]);

  const handleSendReminder = (record) => {
    Modal.confirm({
      title: 'Gửi email nhắc gia hạn?',
      icon: <MailOutlined style={{ color: '#0284c7' }} />,
      content: (
        <div>
          <p>Bạn có chắc chắn muốn gửi email nhắc gia hạn thẻ tới hội viên:</p>
          <p><strong>{record.customerName}</strong> ({record.email})</p>
          <p style={{ color: '#ef4444' }}>Thẻ sẽ hết hạn vào ngày <strong>{record.expiryDate}</strong> (còn {record.daysRemaining} ngày).</p>
        </div>
      ),
      okText: 'Gửi email',
      cancelText: 'Hủy',
      onOk: async () => {
        setSendingEmailId(record.id);
        // Giả lập gửi email thành công
        setTimeout(() => {
          setSendingEmailId(null);
          antMessage.success(`Đã gửi email nhắc gia hạn tới ${record.customerName} (${record.email}) thành công!`);
        }, 800);
      }
    });
  };

  const handleExportCSV = () => {
    if (!data?.memberWarnings?.length) {
      antMessage.warning('Không có dữ liệu để xuất!');
      return;
    }
    const headers = ['Hội viên', 'Email', 'Số điện thoại', 'Gói vé', 'Ngày kích hoạt', 'Ngày hết hạn', 'Số ngày còn lại', 'Trạng thái', 'Lượt đã bơi'];
    const rows = data.memberWarnings.map(m => [
      `"${m.customerName}"`,
      `"${m.email || ''}"`,
      `"${m.phone || ''}"`,
      `"${m.packageName}"`,
      `"${m.issueDate}"`,
      `"${m.expiryDate}"`,
      m.daysRemaining,
      `"${m.status}"`,
      m.totalEntriesUsed
    ]);

    const csvContent = '\uFEFF' + [headers.join(','), ...rows.map(e => e.join(','))].join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', `danh_sach_hoi_vien_ve_thang_${filter}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  // ============ BẢNG DANH SÁCH CẢNH BÁO ============
  const columns = [
    {
      title: 'Hội viên',
      dataIndex: 'customerName',
      key: 'customerName',
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
      key: 'phone',
      render: (phone) => <span style={{ fontWeight: 500 }}>{phone || '—'}</span>,
    },
    {
      title: 'Gói vé sở hữu',
      dataIndex: 'packageName',
      key: 'packageName',
      render: (pkg) => {
        const isChild = pkg?.includes('trẻ em');
        return <Tag color={isChild ? 'cyan' : 'purple'}>{pkg}</Tag>;
      },
    },
    {
      title: 'Ngày hết hạn',
      dataIndex: 'expiryDate',
      key: 'expiryDate',
      render: (date, record) => {
        const isUrgent = record.daysRemaining <= 7 && record.daysRemaining >= 0;
        const isExpired = record.daysRemaining < 0;
        return (
          <div>
            <Text strong style={{ color: isExpired ? '#94a3b8' : (isUrgent ? '#ef4444' : '#0f172a') }}>
              {date}
            </Text>
            <br />
            <Text style={{ fontSize: 12, color: isExpired ? '#ef4444' : (isUrgent ? '#f59e0b' : '#64748b') }}>
              {isExpired ? `Quá hạn ${Math.abs(record.daysRemaining)} ngày` : `Còn ${record.daysRemaining} ngày`}
            </Text>
          </div>
        );
      },
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      key: 'status',
      align: 'center',
      render: (status) => {
        const statusMap = {
          'ACTIVE': { color: 'success', label: 'Đang hiệu lực' },
          'EXPIRING_SOON': { color: 'warning', label: 'Sắp hết hạn' },
          'EXPIRED': { color: 'error', label: 'Đã hết hạn' },
        };
        const conf = statusMap[status] || { color: 'default', label: status };
        return <Tag color={conf.color}>{conf.label}</Tag>;
      },
    },
    {
      title: 'Lượt đã bơi',
      dataIndex: 'totalEntriesUsed',
      key: 'totalEntriesUsed',
      align: 'center',
      width: 100,
      render: (val) => <span style={{ fontWeight: 600, color: '#0284c7' }}>{val} buổi</span>,
    },
    {
      title: 'Hành động',
      key: 'action',
      align: 'center',
      width: 140,
      render: (_, record) => (
        <Button
          type="primary"
          ghost
          size="small"
          icon={<MailOutlined />}
          loading={sendingEmailId === record.id}
          onClick={() => handleSendReminder(record)}
          style={{ borderColor: '#0284c7', color: '#0284c7', borderRadius: 6 }}
        >
          Nhắc gia hạn
        </Button>
      ),
    },
  ];

  return (
    <div style={{ padding: 24, minHeight: '100vh', background: '#f8fafc' }}>
      {/* Page Header */}
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
            💳 Thống Kê Hội Viên Vé Tháng & Vé Định Kỳ
          </Title>
          <Text type="secondary" style={{ fontSize: 14 }}>
            Theo dõi hội viên đang hoạt động, tỷ lệ gia hạn định kỳ và cảnh báo thẻ sắp hết hạn
          </Text>
        </div>

        <Space wrap>
          <Radio.Group
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            buttonStyle="solid"
          >
            <Radio.Button value="all">Tất cả hội viên</Radio.Button>
            <Radio.Button value="active">Đang hiệu lực</Radio.Button>
            <Radio.Button value="expiring_soon">Sắp hết hạn (7 ngày)</Radio.Button>
            <Radio.Button value="expired">Đã hết hạn</Radio.Button>
          </Radio.Group>

          <Button
            icon={<ReloadOutlined />}
            onClick={fetchData}
            loading={loading}
          >
            Làm mới
          </Button>

          <Button
            type="primary"
            icon={<DownloadOutlined />}
            onClick={handleExportCSV}
            style={{ background: '#005f8e', borderColor: '#005f8e' }}
          >
            Xuất danh sách
          </Button>
        </Space>
      </div>

      <Spin spinning={loading}>
        {/* 4 KPI Cards */}
        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
          {/* Card 1: Tổng hội viên */}
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
                title={<span style={{ color: '#475569', fontWeight: 600 }}>TỔNG HỘI VIÊN VÉ THÁNG</span>}
                value={data?.totalMembers || 0}
                prefix={<IdcardOutlined style={{ color: '#0284c7', marginRight: 8 }} />}
                suffix={<span style={{ fontSize: 13, color: '#64748b' }}>người</span>}
              />
              <div style={{ marginTop: 8, fontSize: 12, color: '#0284c7' }}>
                +{data?.newMembersThisMonth || 0} hội viên mới đăng ký trong tháng
              </div>
            </Card>
          </Col>

          {/* Card 2: Thẻ đang hoạt động */}
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
                title={<span style={{ color: '#475569', fontWeight: 600 }}>THẺ ĐANG HOẠT ĐỘNG (ACTIVE)</span>}
                value={data?.activeTickets || 0}
                prefix={<CheckCircleOutlined style={{ color: '#16a34a', marginRight: 8 }} />}
                suffix={<span style={{ fontSize: 13, color: '#64748b' }}>thẻ còn hạn</span>}
              />
              <div style={{ marginTop: 8, fontSize: 12, color: '#16a34a' }}>
                {data?.activePercentage || 0}% tổng số hội viên đăng ký
              </div>
            </Card>
          </Col>

          {/* Card 3: Sắp hết hạn */}
          <Col xs={24} sm={12} lg={6}>
            <Card
              bordered={false}
              style={{
                borderRadius: 12,
                boxShadow: '0 2px 10px rgba(0,0,0,0.04)',
                background: 'linear-gradient(135deg, #ffffff 0%, #fffbeb 100%)',
                borderLeft: '4px solid #f59e0b'
              }}
            >
              <Statistic
                title={<span style={{ color: '#475569', fontWeight: 600 }}>SẮP HẾT HẠN (TRONG 7 NGÀY)</span>}
                value={data?.expiringSoonTickets || 0}
                prefix={<AlertOutlined style={{ color: '#f59e0b', marginRight: 8 }} />}
                suffix={<span style={{ fontSize: 13, color: '#64748b' }}>thẻ</span>}
                valueStyle={{ color: '#d97706' }}
              />
              <div style={{ marginTop: 8, fontSize: 12, color: '#d97706', fontWeight: 500 }}>
                Cần gửi email nhắc gia hạn
              </div>
            </Card>
          </Col>

          {/* Card 4: Tỷ lệ gia hạn thành công */}
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
                title={<span style={{ color: '#475569', fontWeight: 600 }}>TỶ LỆ GIA HẠN THÀNH CÔNG</span>}
                value={data?.renewalRate || 0}
                prefix={<SyncOutlined style={{ color: '#8b5cf6', marginRight: 8 }} />}
                suffix="%"
                precision={1}
              />
              <div style={{ marginTop: 8, fontSize: 12, color: '#8b5cf6' }}>
                Mức độ hài lòng của hội viên cao
              </div>
            </Card>
          </Col>
        </Row>

        {/* Khối Biểu Đồ: Đăng ký mới/Gia hạn & Cơ cấu gói vé */}
        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
          {/* Biểu đồ cột: Xu hướng theo từng tháng */}
          <Col xs={24} lg={16}>
            <Card
              title={
                <Space>
                  <BarChartOutlined style={{ color: '#005f8e' }} />
                  <span>Số lượng đăng ký mới và gia hạn vé tháng theo từng tháng</span>
                </Space>
              }
              bordered={false}
              style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}
            >
              <div style={{ width: '100%', height: 320 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart
                    data={data?.monthlyTrends || []}
                    margin={{ top: 10, right: 20, left: 0, bottom: 0 }}
                  >
                    <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                    <XAxis dataKey="month" stroke="#64748b" />
                    <YAxis stroke="#64748b" />
                    <RechartsTooltip content={<MonthlyTrendTooltip />} />
                    <Legend verticalAlign="bottom" height={36} />
                    <Bar
                      dataKey="renewedMembers"
                      name="Hội viên gia hạn"
                      fill="#005f8e"
                      radius={[4, 4, 0, 0]}
                    />
                    <Bar
                      dataKey="newMembers"
                      name="Hội viên đăng ký mới"
                      fill="#10b981"
                      radius={[4, 4, 0, 0]}
                    />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </Card>
          </Col>

          {/* Biểu đồ tròn: Cơ cấu gói vé định kỳ */}
          <Col xs={24} lg={8}>
            <Card
              title={
                <Space>
                  <PieChartOutlined style={{ color: '#8b5cf6' }} />
                  <span>Cơ cấu các gói vé định kỳ</span>
                </Space>
              }
              bordered={false}
              style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}
            >
              <div style={{ width: '100%', height: 320 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={data?.packageDistributions || []}
                      cx="50%"
                      cy="45%"
                      innerRadius={60}
                      outerRadius={95}
                      paddingAngle={4}
                      dataKey="value"
                      nameKey="name"
                      label={({ percent }) => `${(percent * 100).toFixed(0)}%`}
                    >
                      {(data?.packageDistributions || []).map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={entry.color} />
                      ))}
                    </Pie>
                    <RechartsTooltip
                      formatter={(val, name) => [`${val} thẻ`, name]}
                    />
                    <Legend verticalAlign="bottom" height={36} />
                  </PieChart>
                </ResponsiveContainer>
              </div>
            </Card>
          </Col>
        </Row>

        {/* Khối Bảng Danh Sách Cảnh Báo Hết Hạn */}
        <Card
          title={
            <Space>
              <AlertOutlined style={{ color: '#d97706' }} />
              <span>Danh sách cảnh báo hội viên sắp hết hạn thẻ trong vòng 7 ngày tới</span>
            </Space>
          }
          bordered={false}
          style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}
        >
          <Table
            rowKey="id"
            dataSource={data?.memberWarnings || []}
            columns={columns}
            pagination={{ pageSize: 8 }}
            size="middle"
            scroll={{ x: 800 }}
          />
        </Card>
      </Spin>
    </div>
  );
}

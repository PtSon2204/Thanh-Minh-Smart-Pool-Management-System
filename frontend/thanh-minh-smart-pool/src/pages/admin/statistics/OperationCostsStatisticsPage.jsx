import { useState, useEffect, useCallback } from 'react';
import {
  Card, Row, Col, Statistic, Radio, Button, Table, Tag, Typography,
  Space, Spin, message as antMessage
} from 'antd';
import {
  ToolOutlined, DollarOutlined, ExperimentOutlined,
  ReloadOutlined, DownloadOutlined, BarChartOutlined, PieChartOutlined,
  CalculatorOutlined
} from '@ant-design/icons';
import {
  BarChart, Bar, PieChart, Pie, Cell,
  XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip,
  ResponsiveContainer
} from 'recharts';
import { reportService } from '../../../features/reports/services/reportService';
import { startSignalRConnection, onReceiveNotification } from '../../../services/signalrClient';

const { Title, Text } = Typography;

// Định dạng tiền tệ VNĐ
const formatVND = (val) => {
  if (val === undefined || val === null) return '0 đ';
  return new Intl.NumberFormat('vi-VN').format(val) + ' đ';
};

// Custom Tooltip cho Biểu đồ biến động chi phí qua các tháng (khai báo ngoài component tránh lint render warning)
const OperationCostTrendTooltip = ({ active, payload, label }) => {
  if (!active || !payload?.length) return null;
  const total = payload.reduce((sum, p) => sum + (Number(p.value) || 0), 0);
  return (
    <div style={{
      background: '#fff', border: '1px solid #cbd5e1',
      borderRadius: 8, padding: '10px 14px',
      boxShadow: '0 4px 12px rgba(0,0,0,0.08)'
    }}>
      <Text strong style={{ color: '#0f172a' }}>Tháng {label}</Text>
      <div style={{ marginTop: 6, display: 'flex', flexDirection: 'column', gap: 4 }}>
        {payload.map((p, idx) => (
          <div key={idx} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ width: 10, height: 10, borderRadius: 2, background: p.color, display: 'inline-block' }} />
              <Text style={{ fontSize: 12 }}>{p.name}:</Text>
            </span>
            <Text strong style={{ fontSize: 12 }}>{formatVND(p.value)}</Text>
          </div>
        ))}
        <div style={{ borderTop: '1px solid #e2e8f0', marginTop: 4, paddingTop: 4, display: 'flex', justifyContent: 'space-between', gap: 12 }}>
          <Text strong style={{ fontSize: 12 }}>Tổng cộng:</Text>
          <Text strong style={{ fontSize: 13, color: '#ef4444' }}>{formatVND(total)}</Text>
        </div>
      </div>
    </div>
  );
};

// Custom Tooltip cho Biểu đồ cơ cấu hạng mục (khai báo ngoài component)
const OperationCategoryTooltip = ({ active, payload }) => {
  if (!active || !payload?.length) return null;
  const item = payload[0];
  return (
    <div style={{
      background: '#fff', border: '1px solid #cbd5e1',
      borderRadius: 8, padding: '10px 14px',
      boxShadow: '0 4px 12px rgba(0,0,0,0.08)'
    }}>
      <Text strong style={{ color: item.payload?.color || '#0f172a' }}>{item.name}</Text>
      <div style={{ marginTop: 4 }}>
        <Text style={{ fontSize: 13 }}>
          Chi phí: <strong>{formatVND(item.value)}</strong> ({item.payload?.percentage}%)
        </Text>
      </div>
    </div>
  );
};

export default function OperationCostsStatisticsPage() {
  const [period, setPeriod] = useState('year');
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState(null);

  const handleReload = useCallback(() => {
    setLoading(true);
    reportService.getOperationCostsStatistics({ period })
      .then((res) => setData(res))
      .catch((err) => antMessage.error(err.response?.data?.message || 'Không thể tải dữ liệu chi phí vận hành'))
      .finally(() => setLoading(false));
  }, [period]);

  useEffect(() => {
    let ignore = false;
    reportService.getOperationCostsStatistics({ period })
      .then((res) => {
        if (!ignore) setData(res);
      })
      .catch((err) => {
        if (!ignore) antMessage.error(err.response?.data?.message || 'Không thể tải dữ liệu chi phí vận hành');
      })
      .finally(() => {
        if (!ignore) setLoading(false);
      });

    return () => {
      ignore = true;
    };
  }, [period]);

  useEffect(() => {
    startSignalRConnection();
    const unsub = onReceiveNotification((notif) => {
      const type = (notif?.type || '').toUpperCase();
      if (type === 'MAINTENANCE' || type === 'OPERATION' || type === 'COST' || type === 'SYSTEM' || type.startsWith('MT:')) {
        handleReload();
      }
    });
    return () => unsub();
  }, [handleReload]);

  // Xuất file CSV bảng chi phí bảo trì & vận hành
  const handleExportCSV = () => {
    if (!data?.recentMaintenanceLogs?.length) {
      antMessage.warning('Không có dữ liệu nhật ký bảo trì để xuất bảng chi phí');
      return;
    }

    const headers = ['Thiết bị bảo trì', 'Hạng mục thực hiện', 'Chi phí bảo dưỡng (VNĐ)', 'Ngày thực hiện', 'Đơn vị thực hiện', 'Đánh giá sau bảo trì'];
    const rows = data.recentMaintenanceLogs.map(l => [
      `"${l.equipmentName}"`,
      `"${l.maintenanceType}"`,
      `"${l.cost}"`,
      `"${l.actualDate}"`,
      `"${l.vendorName}"`,
      `"${(l.evaluation || '').replace(/"/g, '""')}"`
    ]);

    const csvContent = '\uFEFF' + [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', `Chi_Phi_Van_Hanh_Bao_Tri_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    antMessage.success('Đã xuất bảng chi phí thành công!');
  };

  // Cấu hình bảng nhật ký chi phí bảo trì
  const columns = [
    {
      title: 'Thiết bị bảo trì',
      dataIndex: 'equipmentName',
      key: 'equipmentName',
      render: (name) => <span style={{ fontWeight: 600, color: '#0f172a' }}>{name}</span>
    },
    {
      title: 'Hạng mục thực hiện',
      dataIndex: 'maintenanceType',
      key: 'maintenanceType',
      render: (type) => (
        <Tag color="cyan" style={{ borderRadius: 4, fontWeight: 500, padding: '2px 8px' }}>
          {type}
        </Tag>
      )
    },
    {
      title: 'Chi phí bảo dưỡng',
      dataIndex: 'formattedCost',
      key: 'formattedCost',
      align: 'right',
      render: (cost) => (
        <span style={{ fontWeight: 700, color: '#dc2626', fontSize: 14 }}>
          {cost}
        </span>
      )
    },
    {
      title: 'Ngày thực hiện',
      dataIndex: 'actualDate',
      key: 'actualDate',
      align: 'center',
      render: (date) => <span style={{ color: '#475569' }}>{date}</span>
    },
    {
      title: 'Đơn vị thực hiện',
      dataIndex: 'vendorName',
      key: 'vendorName',
      render: (vendor) => <span style={{ fontWeight: 500, color: '#334155' }}>{vendor}</span>
    },
    {
      title: 'Đánh giá sau bảo trì',
      dataIndex: 'evaluation',
      key: 'evaluation',
      render: (evalText) => (
        <Tag color="success" style={{ borderRadius: 4, padding: '2px 8px', color: '#15803d' }}>
          {evalText}
        </Tag>
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
          <Title level={3} style={{ margin: 0, color: '#0f172a', display: 'flex', alignItems: 'center', gap: 10 }}>
            <ToolOutlined style={{ color: '#0284c7', fontSize: 26 }} />
            Thống Kê Chi Phí Vận Hành & Bảo Trì Thiết Bị
          </Title>
          <Text type="secondary" style={{ fontSize: 14, marginTop: 4, display: 'block' }}>
            Theo dõi ngân sách mua sắm hóa chất, tiền điện nước, bảo dưỡng máy móc và chi phí sửa chữa thay thế
          </Text>
        </div>

        {/* TOP RIGHT CONTROLS */}
        <Space size="middle" wrap>
          <Radio.Group
            value={period}
            onChange={(e) => setPeriod(e.target.value)}
            buttonStyle="solid"
          >
            <Radio.Button value="month">Tháng này</Radio.Button>
            <Radio.Button value="quarter">Quý này</Radio.Button>
            <Radio.Button value="year">Cả năm 2026</Radio.Button>
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
            style={{ background: '#0284c7', borderColor: '#0284c7' }}
          >
            Xuất bảng chi phí
          </Button>
        </Space>
      </div>

      <Spin spinning={loading}>
        {/* 4 KPI CARDS */}
        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
          {/* KPI 1: TỔNG CHI PHÍ */}
          <Col xs={24} sm={12} lg={6}>
            <Card
              bordered={false}
              style={{
                borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
                borderLeft: '5px solid #ef4444'
              }}
              bodyStyle={{ padding: '20px 24px' }}
            >
              <Text type="secondary" style={{ fontSize: 13, textTransform: 'uppercase', fontWeight: 600, letterSpacing: 0.5 }}>
                TỔNG CHI PHÍ VẬN HÀNH & BẢO TRÌ
              </Text>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: 8 }}>
                <Statistic
                  value={data ? formatVND(data.totalCosts) : '142.500.000 đ'}
                  valueStyle={{ fontSize: 24, fontWeight: 700, color: '#ef4444' }}
                />
                <div style={{
                  width: 48, height: 48, borderRadius: 12,
                  background: '#fee2e2', display: 'flex', alignItems: 'center', justifyContent: 'center'
                }}>
                  <DollarOutlined style={{ fontSize: 24, color: '#ef4444' }} />
                </div>
              </div>
              <div style={{ marginTop: 8, fontSize: 13, color: '#64748b' }}>
                Kiểm soát trong hạn mức ngân sách
              </div>
            </Card>
          </Col>

          {/* KPI 2: BẢO DƯỠNG MÁY MÓC */}
          <Col xs={24} sm={12} lg={6}>
            <Card
              bordered={false}
              style={{
                borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
                borderLeft: '5px solid #0284c7'
              }}
              bodyStyle={{ padding: '20px 24px' }}
            >
              <Text type="secondary" style={{ fontSize: 13, textTransform: 'uppercase', fontWeight: 600, letterSpacing: 0.5 }}>
                CHI PHÍ BẢO DƯỠNG MÁY MÓC
              </Text>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: 8 }}>
                <Statistic
                  value={data ? formatVND(data.equipmentMaintenanceCost) : '45.600.000 đ'}
                  valueStyle={{ fontSize: 24, fontWeight: 700, color: '#0f172a' }}
                />
                <div style={{
                  width: 48, height: 48, borderRadius: 12,
                  background: '#e0f2fe', display: 'flex', alignItems: 'center', justifyContent: 'center'
                }}>
                  <ToolOutlined style={{ fontSize: 24, color: '#0284c7' }} />
                </div>
              </div>
              <div style={{ marginTop: 8, fontSize: 13, color: '#0284c7' }}>
                Bơm, bình lọc, đèn và máy gia nhiệt
              </div>
            </Card>
          </Col>

          {/* KPI 3: HÓA CHẤT XỬ LÝ NƯỚC */}
          <Col xs={24} sm={12} lg={6}>
            <Card
              bordered={false}
              style={{
                borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
                borderLeft: '5px solid #0ea5e9'
              }}
              bodyStyle={{ padding: '20px 24px' }}
            >
              <Text type="secondary" style={{ fontSize: 13, textTransform: 'uppercase', fontWeight: 600, letterSpacing: 0.5 }}>
                HÓA CHẤT XỬ LÝ NƯỚC
              </Text>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: 8 }}>
                <Statistic
                  value={data ? formatVND(data.waterTreatmentCost) : '38.200.000 đ'}
                  valueStyle={{ fontSize: 24, fontWeight: 700, color: '#0f172a' }}
                />
                <div style={{
                  width: 48, height: 48, borderRadius: 12,
                  background: '#e0f2fe', display: 'flex', alignItems: 'center', justifyContent: 'center'
                }}>
                  <ExperimentOutlined style={{ fontSize: 24, color: '#0ea5e9' }} />
                </div>
              </div>
              <div style={{ marginTop: 8, fontSize: 13, color: '#0369a1' }}>
                Đảm bảo độ trong & khử trùng nước chuẩn
              </div>
            </Card>
          </Col>

          {/* KPI 4: TỶ LỆ CHI PHÍ / DOANH THU */}
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
                TỶ LỆ CHI PHÍ / DOANH THU
              </Text>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: 8 }}>
                <Statistic
                  value={data?.costToRevenueRatio ?? 24.5}
                  precision={1}
                  suffix={<span style={{ fontSize: 18, fontWeight: 700, color: '#10b981' }}>%</span>}
                  valueStyle={{ fontSize: 28, fontWeight: 700, color: '#10b981' }}
                />
                <div style={{
                  width: 48, height: 48, borderRadius: 12,
                  background: '#dcfce7', display: 'flex', alignItems: 'center', justifyContent: 'center'
                }}>
                  <CalculatorOutlined style={{ fontSize: 24, color: '#10b981' }} />
                </div>
              </div>
              <div style={{ marginTop: 8, fontSize: 13, color: '#15803d' }}>
                Mức tối ưu đạt chuẩn vận hành
              </div>
            </Card>
          </Col>
        </Row>

        {/* 2 CHARTS ROW */}
        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
          {/* STACKED BAR CHART */}
          <Col xs={24} lg={14}>
            <Card
              title={
                <span style={{ fontSize: 15, fontWeight: 600, color: '#0f172a', display: 'flex', alignItems: 'center', gap: 8 }}>
                  <BarChartOutlined style={{ color: '#0284c7' }} />
                  Biến động chi phí vận hành & bảo trì qua các tháng (VNĐ)
                </span>
              }
              bordered={false}
              style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.04)', height: '100%' }}
            >
              <div style={{ width: '100%', height: 280 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart
                    data={data?.monthlyTrends || []}
                    margin={{ top: 15, right: 20, left: 10, bottom: 0 }}
                  >
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
                      tick={{ fontSize: 11 }}
                      tickFormatter={(v) => `${(v / 1_000_000).toFixed(1).replace('.0', '')}tr`}
                    />
                    <RechartsTooltip content={<OperationCostTrendTooltip />} />
                    <Bar dataKey="equipmentCost" name="Bảo trì máy móc" stackId="cost" fill="#085078" />
                    <Bar dataKey="chemicalCost" name="Hóa chất xử lý nước" stackId="cost" fill="#0284c7" />
                    <Bar dataKey="electricityWaterCost" name="Điện nước vận hành" stackId="cost" fill="#f59e0b" radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </div>

              {/* Stacked Chart Legend */}
              <div style={{ display: 'flex', justifyContent: 'center', gap: 24, marginTop: 12, flexWrap: 'wrap' }}>
                <span style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, color: '#475569' }}>
                  <span style={{ width: 12, height: 12, borderRadius: 2, background: '#085078' }} />
                  Bảo trì máy móc
                </span>
                <span style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, color: '#475569' }}>
                  <span style={{ width: 12, height: 12, borderRadius: 2, background: '#0284c7' }} />
                  Hóa chất xử lý nước
                </span>
                <span style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, color: '#475569' }}>
                  <span style={{ width: 12, height: 12, borderRadius: 2, background: '#f59e0b' }} />
                  Điện nước vận hành
                </span>
              </div>
            </Card>
          </Col>

          {/* DONUT CHART */}
          <Col xs={24} lg={10}>
            <Card
              title={
                <span style={{ fontSize: 15, fontWeight: 600, color: '#0f172a', display: 'flex', alignItems: 'center', gap: 8 }}>
                  <PieChartOutlined style={{ color: '#0ea5e9' }} />
                  Cơ cấu các hạng mục chi phí
                </span>
              }
              bordered={false}
              style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.04)', height: '100%' }}
            >
              <div style={{ width: '100%', height: 250 }}>
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
                    <RechartsTooltip content={<OperationCategoryTooltip />} />
                  </PieChart>
                </ResponsiveContainer>
              </div>

              {/* Donut Chart Legend */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 10 }}>
                {(data?.categoryDistributions || []).map((cat, i) => (
                  <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <span style={{
                      width: 12, height: 12, borderRadius: 2,
                      background: cat.color, flexShrink: 0
                    }} />
                    <span style={{ fontSize: 12, color: '#334155', lineHeight: 1.3 }}>
                      {cat.name} ({cat.percentage}%)
                    </span>
                  </div>
                ))}
              </div>
            </Card>
          </Col>
        </Row>

        {/* TABLE SECTION */}
        <Card
          title={
            <span style={{ fontSize: 16, fontWeight: 600, color: '#0f172a', display: 'flex', alignItems: 'center', gap: 8 }}>
              <ToolOutlined style={{ color: '#0284c7' }} />
              Nhật ký chi phí bảo trì & sửa chữa thiết bị bể bơi
            </span>
          }
          bordered={false}
          style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.04)' }}
          bodyStyle={{ padding: 0 }}
        >
          <Table
            columns={columns}
            scroll={{ x: 800 }}
            dataSource={data?.recentMaintenanceLogs || []}
            rowKey="id"
            pagination={{
              pageSize: 6,
              showTotal: (total) => `Tổng số ${total} hạng mục bảo trì`
            }}
            locale={{ emptyText: 'Chưa có nhật ký bảo trì nào trong kỳ này' }}
          />
        </Card>
      </Spin>
    </div>
  );
}

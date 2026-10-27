import React, { useState, useEffect } from 'react';
import {
  Row,
  Col,
  Card,
  Statistic,
  DatePicker,
  Table,
  Tag,
  Space,
  Typography,
  Spin,
  Alert,
  Empty,
  Button,
} from 'antd';
import {
  CheckCircleOutlined,
  WarningOutlined,
  CloseCircleOutlined,
  ExperimentOutlined,
  DashboardOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ReferenceArea,
  ReferenceLine,
} from 'recharts';
import dayjs from 'dayjs';
import { waterQualityService } from '../services/waterQualityService';

const { RangePicker } = DatePicker;
const { Title, Text } = Typography;

export default function WaterQualityAnalyticsTab({ onViewDetail }) {
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState(null);
  const [dateRange, setDateRange] = useState([dayjs().subtract(30, 'day'), dayjs()]);

  const fetchAnalytics = async () => {
    try {
      setLoading(true);
      const params = {};
      if (dateRange && dateRange[0] && dateRange[1]) {
        params.fromDate = dateRange[0].startOf('day').toISOString();
        params.toDate = dateRange[1].endOf('day').toISOString();
      }
      const res = await waterQualityService.getAnalytics(params);
      setData(res);
    } catch (err) {
      console.error('Lỗi tải thống kê chất lượng nước:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchAnalytics();
  }, [dateRange]);

  const alertColumns = [
    {
      title: 'Thời điểm',
      dataIndex: 'inspectionTime',
      key: 'inspectionTime',
      render: (t) => dayjs(t).format('DD/MM/YYYY HH:mm'),
    },
    {
      title: 'pH',
      dataIndex: 'phValue',
      key: 'phValue',
      render: (v) => (
        <Text strong style={{ color: v < 7.2 || v > 7.8 ? '#cf1322' : '#389e0d' }}>
          {Number(v).toFixed(2)}
        </Text>
      ),
    },
    {
      title: 'Clo tự do (mg/L)',
      dataIndex: 'freeChlorineMgL',
      key: 'freeChlorineMgL',
      render: (v) => (
        <Text strong style={{ color: v < 1.0 || v > 3.0 ? '#cf1322' : '#389e0d' }}>
          {Number(v).toFixed(2)}
        </Text>
      ),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      key: 'status',
      render: (s) => (
        <Tag color={s === 'Danger' ? 'error' : 'warning'}>
          {s === 'Danger' ? 'Nguy hiểm' : 'Cảnh báo'}
        </Tag>
      ),
    },
    {
      title: 'Khuyến nghị hóa chất',
      dataIndex: 'recommendation',
      key: 'recommendation',
      ellipsis: true,
    },
    {
      title: 'Thao tác',
      key: 'action',
      render: (_, record) => (
        <Button size="small" type="link" onClick={() => onViewDetail(record)}>
          Xem chi tiết
        </Button>
      ),
    },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      {/* Filter toolbar */}
      <Card size="small">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
          <Space wrap>
            <Text strong>Khoảng thời gian phân tích:</Text>
            <RangePicker
              value={dateRange}
              onChange={(dates) => setDateRange(dates)}
              format="DD/MM/YYYY"
              allowClear={false}
            />
          </Space>
          <Button icon={<ReloadOutlined />} onClick={fetchAnalytics} loading={loading}>
            Làm mới dữ liệu
          </Button>
        </div>
      </Card>

      {loading && !data ? (
        <div style={{ textAlign: 'center', padding: '60px 0' }}>
          <Spin size="large" tip="Đang tải dữ liệu phân tích chỉ số nước..." />
        </div>
      ) : data ? (
        <>
          {/* KPI Statistics cards */}
          <Row gutter={[16, 16]}>
            <Col xs={24} sm={12} md={6}>
              <Card>
                <Statistic
                  title="Tổng số lượt kiểm tra"
                  value={data.totalLogs}
                  prefix={<DashboardOutlined style={{ color: '#1890ff' }} />}
                  suffix="lần"
                />
              </Card>
            </Col>
            <Col xs={24} sm={12} md={6}>
              <Card>
                <Statistic
                  title="Tỷ lệ đạt chuẩn lý tưởng"
                  value={data.complianceRatePercentage}
                  precision={1}
                  prefix={<CheckCircleOutlined style={{ color: '#52c41a' }} />}
                  suffix="%"
                  valueStyle={{ color: '#3f8600' }}
                />
              </Card>
            </Col>
            <Col xs={24} sm={12} md={6}>
              <Card>
                <Statistic
                  title="Số lần cảnh báo lệch ngưỡng"
                  value={data.warningCount}
                  prefix={<WarningOutlined style={{ color: '#faad14' }} />}
                  suffix="lần"
                  valueStyle={{ color: '#d46b08' }}
                />
              </Card>
            </Col>
            <Col xs={24} sm={12} md={6}>
              <Card>
                <Statistic
                  title="Số lần nguy hiểm khẩn cấp"
                  value={data.dangerCount}
                  prefix={<CloseCircleOutlined style={{ color: '#ff4d4f' }} />}
                  suffix="lần"
                  valueStyle={{ color: '#cf1322' }}
                />
              </Card>
            </Col>
          </Row>

          {/* Average metrics overview */}
          <Row gutter={[16, 16]}>
            <Col xs={24} md={12}>
              <Card size="small" title="Chỉ số pH trung bình kỳ đo">
                <div style={{ display: 'flex', justifyContent: 'space-around', textAlign: 'center', padding: '12px 0' }}>
                  <div>
                    <Text type="secondary">Trung bình</Text>
                    <Title level={3} style={{ margin: 0, color: '#1890ff' }}>
                      {data.avgPh ?? '—'}
                    </Title>
                  </div>
                  <div>
                    <Text type="secondary">Thấp nhất</Text>
                    <Title level={4} style={{ margin: 0, color: data.minPh < 7.2 ? '#cf1322' : '#595959' }}>
                      {data.minPh ?? '—'}
                    </Title>
                  </div>
                  <div>
                    <Text type="secondary">Cao nhất</Text>
                    <Title level={4} style={{ margin: 0, color: data.maxPh > 7.8 ? '#cf1322' : '#595959' }}>
                      {data.maxPh ?? '—'}
                    </Title>
                  </div>
                </div>
                <Alert
                  type="info"
                  message="Ngưỡng lý tưởng của pH bể bơi là 7.20 – 7.80. Dưới 6.80 hoặc trên 8.20 là mức Nguy hiểm."
                  showIcon
                  style={{ fontSize: 12, marginTop: 8 }}
                />
              </Card>
            </Col>

            <Col xs={24} md={12}>
              <Card size="small" title="Chỉ số Clo tự do trung bình kỳ đo">
                <div style={{ display: 'flex', justifyContent: 'space-around', textAlign: 'center', padding: '12px 0' }}>
                  <div>
                    <Text type="secondary">Trung bình</Text>
                    <Title level={3} style={{ margin: 0, color: '#13c2c2' }}>
                      {data.avgChlorine ? `${data.avgChlorine} mg/L` : '—'}
                    </Title>
                  </div>
                  <div>
                    <Text type="secondary">Thấp nhất</Text>
                    <Title level={4} style={{ margin: 0, color: data.minChlorine < 1.0 ? '#cf1322' : '#595959' }}>
                      {data.minChlorine ? `${data.minChlorine} mg/L` : '—'}
                    </Title>
                  </div>
                  <div>
                    <Text type="secondary">Cao nhất</Text>
                    <Title level={4} style={{ margin: 0, color: data.maxChlorine > 3.0 ? '#cf1322' : '#595959' }}>
                      {data.maxChlorine ? `${data.maxChlorine} mg/L` : '—'}
                    </Title>
                  </div>
                </div>
                <Alert
                  type="info"
                  message="Ngưỡng lý tưởng của Clo tự do là 1.00 – 3.00 mg/L. Dưới 0.50 hoặc trên 5.00 mg/L là mức Nguy hiểm."
                  showIcon
                  style={{ fontSize: 12, marginTop: 8 }}
                />
              </Card>
            </Col>
          </Row>

          {/* Trend Chart (pH & Clo) */}
          <Card
            title={
              <Space>
                <ExperimentOutlined style={{ color: '#1890ff' }} />
                <span>Biểu đồ biến thiên pH & Nồng độ Clo tự do theo ngày</span>
              </Space>
            }
          >
            {data.dailyTrends && data.dailyTrends.length > 0 ? (
              <div style={{ width: '100%', height: 350 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={data.dailyTrends} margin={{ top: 10, right: 30, left: 0, bottom: 20 }}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis
                      dataIndex="date"
                      dataKey="date"
                      tickFormatter={(val) => dayjs(val).format('DD/MM')}
                    />
                    <YAxis yAxisId="left" domain={[5.5, 9.5]} label={{ value: 'Độ pH', angle: -90, position: 'insideLeft' }} />
                    <YAxis yAxisId="right" orientation="right" domain={[0, 6]} label={{ value: 'Clo (mg/L)', angle: 90, position: 'insideRight' }} />
                    <Tooltip
                      labelFormatter={(label) => `Ngày ${dayjs(label).format('DD/MM/YYYY')}`}
                      formatter={(val, name) => [val, name === 'avgPh' ? 'pH Trung bình' : 'Clo tự do (mg/L)']}
                    />
                    <Legend verticalAlign="top" height={36} />

                    {/* Vùng an toàn của pH */}
                    <ReferenceArea yAxisId="left" y1={7.2} y2={7.8} fill="#52c41a" fillOpacity={0.12} />
                    {/* Vùng an toàn của Clo */}
                    <ReferenceLine yAxisId="right" y={1.0} stroke="#13c2c2" strokeDasharray="3 3" />
                    <ReferenceLine yAxisId="right" y={3.0} stroke="#13c2c2" strokeDasharray="3 3" />

                    <Line
                      yAxisId="left"
                      type="monotone"
                      dataKey="avgPh"
                      name="pH Trung bình"
                      stroke="#1890ff"
                      strokeWidth={2.5}
                      dot={{ r: 4 }}
                      activeDot={{ r: 7 }}
                    />
                    <Line
                      yAxisId="right"
                      type="monotone"
                      dataKey="avgChlorine"
                      name="Clo tự do (mg/L)"
                      stroke="#52c41a"
                      strokeWidth={2.5}
                      dot={{ r: 4 }}
                      activeDot={{ r: 7 }}
                    />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            ) : (
              <Empty description="Chưa có đủ dữ liệu chuỗi ngày để vẽ biểu đồ." />
            )}
          </Card>

          {/* Recent Alerts Table */}
          <Card
            title={
              <Space>
                <WarningOutlined style={{ color: '#faad14' }} />
                <span>Danh sách các lần đo lệch chuẩn / nguy hiểm gần đây</span>
              </Space>
            }
          >
            <Table
              dataSource={data.recentAlerts || []}
              columns={alertColumns}
              rowKey="id"
              pagination={false}
              size="small"
              locale={{ emptyText: 'Không có cảnh báo lệch chuẩn nào trong giai đoạn này. Nước hồ đạt chuẩn rất tốt!' }}
            />
          </Card>
        </>
      ) : null}
    </div>
  );
}

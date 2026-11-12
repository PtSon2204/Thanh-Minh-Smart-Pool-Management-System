import { useState, useEffect, useCallback } from 'react';
import {
  Form,
  Input,
  Button,
  Card,
  Select,
  Radio,
  Alert,
  Table,
  Tag,
  Space,
  Typography,
  message,
} from 'antd';
import {
  SendOutlined,
  MailOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import { notificationService } from '../services/notificationService';

const { Text } = Typography;
const { TextArea } = Input;
const { Option } = Select;

const PRESETS = {
  announcement: {
    subject: '[Thanh Minh Smart Pool] Thông báo tạm dừng hoạt động để bảo trì hệ thống lọc nước',
    body: 'Kính gửi quý khách và toàn thể nhân viên,\n\nBan quản lý xin thông báo khu vực bể bơi sẽ tạm dừng hoạt động từ 13h00 đến 17h00 ngày hôm nay để thực hiện sục rửa hệ thống lọc nước Ozone và kiểm tra kỹ thuật định kỳ. Xin cảm ơn sự thông cảm và phối hợp của quý vị!',
    audience: 'all',
  },
  event_promo: {
    subject: '[Thanh Minh Smart Pool] Tri ân khách hàng - Ưu đãi đặc biệt mùa hè',
    body: 'Kính gửi quý khách hàng,\n\nNhằm tri ân sự tin tưởng và gắn bó của quý khách, Bể bơi Thông minh Thanh Minh gửi tặng ưu đãi giảm 15% khi gia hạn vé tháng hoặc đăng ký khóa học bơi mới trong tuần này. Chi tiết xin liên hệ quầy lễ tân hoặc hotline ban quản lý!',
    audience: 'customers',
  },
  custom: {
    subject: '[Thanh Minh Smart Pool] Thư thông báo từ Ban quản lý bể bơi',
    body: 'Kính gửi quý khách,\n\nBan quản lý xin gửi thông tin quan trọng liên quan đến hoạt động tại bể bơi...',
    audience: 'single',
  },
};

export default function EmailNotificationManager() {
  const [form] = Form.useForm();
  const [historyLoading, setHistoryLoading] = useState(false);
  const [sendLoading, setSendLoading] = useState(false);
  const [historyData, setHistoryData] = useState([]);
  const [selectedAudience, setSelectedAudience] = useState('all');

  const applyPreset = useCallback(
    (presetKey) => {
      const selected = PRESETS[presetKey];
      if (selected) {
        form.setFieldsValue({
          subject: selected.subject,
          body: selected.body,
          targetAudience: selected.audience,
        });
        setSelectedAudience(selected.audience);
      }
    },
    [form]
  );

  const fetchHistory = useCallback(async () => {
    try {
      setHistoryLoading(true);
      const res = await notificationService.getEmailHistory(50);
      setHistoryData(Array.isArray(res) ? res : res?.value || []);
    } catch (error) {
      message.error(error?.message || 'Không thể tải lịch sử gửi email');
    } finally {
      setHistoryLoading(false);
    }
  }, []);

  useEffect(() => {
    let isMounted = true;
    notificationService.getEmailHistory(50)
      .then((res) => {
        if (isMounted) {
          setHistoryData(Array.isArray(res) ? res : res?.value || []);
        }
      })
      .catch((error) => {
        if (isMounted) {
          message.error(error?.message || 'Không thể tải lịch sử gửi email');
        }
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const handleTemplateChange = (val) => {
    applyPreset(val);
  };

  const handleSend = async (values) => {
    try {
      setSendLoading(true);
      const target = values.targetAudience || 'single';
      const res = await notificationService.sendEmailNotification({
        targetAudience: target,
        toEmail: target === 'single' ? values.recipient : undefined,
        subject: values.subject,
        content: values.body,
        templateType: values.template,
      });

      if (res?.success) {
        message.success(res.message || 'Email đã được gửi thành công!');
        form.resetFields();
        applyPreset('announcement');
        await fetchHistory();
      } else {
        message.error(res?.message || 'Gửi email thất bại');
      }
    } catch (error) {
      message.error(error?.message || 'Gửi email thất bại');
    } finally {
      setSendLoading(false);
    }
  };

  const columns = [
    {
      title: 'Người nhận',
      dataIndex: 'recipient',
      key: 'recipient',
    },
    {
      title: 'Mẫu thông báo',
      dataIndex: 'templateType',
      key: 'templateType',
      render: (type) => {
        if (type === 'ticket_reminder') return <Tag color="orange">Tự động: Nhắc vé 3 ngày</Tag>;
        if (type === 'ticket_confirmation') return <Tag color="blue">Tự động: Xác nhận vé</Tag>;
        if (type === 'announcement') return <Tag color="purple">Phát thanh: Bảo trì</Tag>;
        if (type === 'event_promo') return <Tag color="magenta">Sự kiện / Khuyến mãi</Tag>;
        return <Tag color="default">{type || 'custom'}</Tag>;
      },
    },
    {
      title: 'Tiêu đề',
      dataIndex: 'subject',
      key: 'subject',
      render: (text) => <Text ellipsis style={{ maxWidth: 220 }}>{text}</Text>,
    },
    {
      title: 'Thời gian gửi',
      dataIndex: 'sentAt',
      key: 'sentAt',
      render: (date) => (date ? new Date(date).toLocaleString('vi-VN') : '—'),
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      key: 'status',
      render: (status) => {
        const color = status?.includes('Thành công') || status === 'SUCCESS' ? 'green' : 'volcano';
        return <Tag color={color}>{status || 'SENT'}</Tag>;
      },
    },
  ];

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Card title="Phát thông báo & Gửi Email Ban quản lý" extra={<MailOutlined />}>
        <Form
          layout="vertical"
          form={form}
          onFinish={handleSend}
          initialValues={{
            template: 'announcement',
            targetAudience: 'all',
            subject: PRESETS.announcement.subject,
            body: PRESETS.announcement.body,
          }}
        >
          <Form.Item name="template" label="Mẫu thông báo">
            <Select onChange={handleTemplateChange}>
              <Option value="announcement">📢 Thông báo bảo trì / sự cố bể bơi</Option>
              <Option value="event_promo">🎉 Thông báo sự kiện / khuyến mãi / tin tức</Option>
              <Option value="custom">✉️ Thư thông báo tùy chỉnh</Option>
            </Select>
          </Form.Item>

          <Form.Item
            name="targetAudience"
            label="Đối tượng nhận thông báo"
            rules={[{ required: true, message: 'Vui lòng chọn đối tượng nhận!' }]}
          >
            <Radio.Group
              value={selectedAudience}
              onChange={(e) => setSelectedAudience(e.target.value)}
              buttonStyle="solid"
            >
              <Radio.Button value="all">Tất cả</Radio.Button>
              <Radio.Button value="customers">Khách hàng</Radio.Button>
              <Radio.Button value="staff">Nhân viên</Radio.Button>
              <Radio.Button value="single">Một email cụ thể</Radio.Button>
            </Radio.Group>
          </Form.Item>

          {selectedAudience === 'single' ? (
            <Form.Item
              name="recipient"
              label="Email người nhận"
              extra="Hệ thống sẽ xác thực email này phải tồn tại trong danh sách tài khoản của bể bơi trước khi gửi."
              rules={[
                { required: true, message: 'Vui lòng nhập email!' },
                { type: 'email', message: 'Email không đúng định dạng!' },
              ]}
            >
              <Input placeholder="example@gmail.com" />
            </Form.Item>
          ) : (
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 20 }}
              message={
                selectedAudience === 'all'
                  ? 'Hệ thống sẽ tự động quét và gửi email thông báo này tới toàn bộ khách hàng và nhân viên trong hệ thống.'
                  : selectedAudience === 'customers'
                    ? 'Hệ thống sẽ gửi email đồng loạt tới toàn bộ tài khoản khách hàng.'
                    : 'Hệ thống sẽ gửi email đồng loạt tới toàn bộ nhân viên bể bơi.'
              }
            />
          )}

          <Form.Item
            name="subject"
            label="Tiêu đề"
            rules={[{ required: true, message: 'Vui lòng nhập tiêu đề email!' }]}
          >
            <Input placeholder="Tiêu đề email..." />
          </Form.Item>

          <Form.Item
            name="body"
            label="Nội dung"
            rules={[{ required: true, message: 'Vui lòng nhập nội dung!' }]}
          >
            <TextArea rows={6} placeholder="Nội dung thông báo..." />
          </Form.Item>

          <Button type="primary" htmlType="submit" icon={<SendOutlined />} loading={sendLoading}>
            Gửi email
          </Button>
        </Form>
      </Card>

      <Card
        title="Lịch sử gửi email"
        extra={
          <Button icon={<ReloadOutlined />} onClick={fetchHistory} loading={historyLoading}>
            Làm mới
          </Button>
        }
      >
        <Table
          rowKey={(record) => record.id || record.sentAt}
          scroll={{ x: 640 }}
          columns={columns}
          dataSource={historyData}
          loading={historyLoading}
          pagination={{ pageSize: 5 }}
        />
      </Card>
    </Space>
  );
}

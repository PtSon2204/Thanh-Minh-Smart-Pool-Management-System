import { useState, useEffect, useCallback } from 'react';
import {
  Form,
  Input,
  Button,
  Card,
  Select,
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
  ticket_confirmation: {
    subject: '[Thanh Minh Smart Pool] Xác nhận đặt vé thành công',
    body: 'Kính gửi quý khách,\n\nCảm ơn quý khách đã mua vé tại Thanh Minh Smart Pool. Mã vé của bạn đã sẵn sàng...',
  },
  renewal_reminder: {
    subject: '[Thanh Minh Smart Pool] Nhắc nhở gia hạn gói thành viên',
    body: 'Kính gửi quý khách,\n\nGói bơi của quý khách sắp hết hạn trong 3 ngày tới. Vui lòng gia hạn để tiếp tục sử dụng dịch vụ...',
  },
};

export default function EmailNotificationManager() {
  const [form] = Form.useForm();
  const [historyLoading, setHistoryLoading] = useState(false);
  const [sendLoading, setSendLoading] = useState(false);
  const [historyData, setHistoryData] = useState([]);

  const applyPreset = useCallback(
    (presetKey) => {
      const selected = PRESETS[presetKey];
      if (selected) {
        form.setFieldsValue({
          subject: selected.subject,
          body: selected.body,
        });
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
      const res = await notificationService.sendEmailNotification({
        toEmail: values.recipient,
        subject: values.subject,
        content: values.body,
        templateType: values.template,
      });

      if (res?.success) {
        message.success(res.message || 'Email đã được gửi thành công!');
        form.resetFields();
        applyPreset('ticket_confirmation');
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
      title: 'Tiêu đề',
      dataIndex: 'subject',
      key: 'subject',
      render: (text) => <Text ellipsis style={{ maxWidth: 200 }}>{text}</Text>,
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
      <Card title="Gửi thông báo Email" extra={<MailOutlined />}>
        <Form
          layout="vertical"
          form={form}
          onFinish={handleSend}
          initialValues={{
            template: 'ticket_confirmation',
            subject: PRESETS.ticket_confirmation.subject,
            body: PRESETS.ticket_confirmation.body,
          }}
        >
          <Form.Item name="template" label="Mẫu thông báo">
            <Select onChange={handleTemplateChange}>
              <Option value="ticket_confirmation">Xác nhận đặt vé</Option>
              <Option value="renewal_reminder">Nhắc nhở gia hạn</Option>
            </Select>
          </Form.Item>

          <Form.Item
            name="recipient"
            label="Email người nhận"
            rules={[
              { required: true, message: 'Vui lòng nhập email!' },
              { type: 'email', message: 'Email không đúng định dạng!' },
            ]}
          >
            <Input placeholder="example@gmail.com" />
          </Form.Item>

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
          columns={columns}
          dataSource={historyData}
          loading={historyLoading}
          pagination={{ pageSize: 5 }}
        />
      </Card>
    </Space>
  );
}
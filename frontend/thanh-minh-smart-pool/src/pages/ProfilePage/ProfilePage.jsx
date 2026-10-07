import { useEffect, useState, useCallback } from 'react';
import { Button, Input, DatePicker, Avatar, Form, message, Spin, Upload } from 'antd';
import { Mail, Phone, Camera } from 'lucide-react';
import { UserOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import profileService from '../../features/profiles/services/profileService';
import './ProfilePage.css';

const ProfilePage = () => {
  const [form] = Form.useForm();
  const queryClient = useQueryClient();
  const [loading, setLoading] = useState(false);
  const [fetching, setFetching] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [currentProfile, setCurrentProfile] = useState(null);

  const fetchProfile = useCallback(async () => {
    try {
      setFetching(true);
      const data = await profileService.getMyProfile();
      setCurrentProfile(data);
      form.setFieldsValue({
        fullName: data.fullName,
        username: data.username,
        address: data.address,
        phone: data.phone,
        dateOfBirth: data.dateOfBirth ? dayjs(data.dateOfBirth) : null
      });
    } catch {
      message.error('Không thể lấy thông tin hồ sơ');
    } finally {
      setFetching(false);
    }
  }, [form]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchProfile();
  }, [fetchProfile]);


  const handleFinish = async (values) => {
    try {
      setLoading(true);
      const payload = {
        fullName: values.fullName,
        address: values.address,
        dateOfBirth: values.dateOfBirth ? values.dateOfBirth.format('YYYY-MM-DD') : null
      };
      await profileService.updateMyProfile(payload);
      message.success('Cập nhật thông tin thành công!');
      
      setCurrentProfile({
        ...currentProfile,
        fullName: payload.fullName,
        address: payload.address,
        dateOfBirth: payload.dateOfBirth
      });
      queryClient.invalidateQueries({ queryKey: ['myProfile'] });
    } catch {
      message.error('Cập nhật thất bại. Vui lòng thử lại.');
    } finally {
      setLoading(false);
    }
  };

  const handleAvatarUpload = async (options) => {
    const { file } = options;
    try {
      setUploading(true);
      const res = await profileService.uploadMyAvatar(file);
      setCurrentProfile({ ...currentProfile, avatarUrl: res.avatarUrl });
      message.success('Cập nhật ảnh đại diện thành công!');
      queryClient.invalidateQueries({ queryKey: ['myProfile'] });
    } catch {
      message.error('Cập nhật ảnh thất bại.');
    } finally {
      setUploading(false);
    }
  };

  const beforeUpload = (file) => {
    const isJpgOrPng = file.type === 'image/jpeg' || file.type === 'image/png' || file.type === 'image/jpg';
    if (!isJpgOrPng) {
      message.error('Bạn chỉ có thể tải lên file JPG/PNG!');
    }
    const isLt5M = file.size / 1024 / 1024 < 5;
    if (!isLt5M) {
      message.error('Ảnh tải lên phải nhỏ hơn 5MB!');
    }
    return isJpgOrPng && isLt5M;
  };

  if (fetching) {
    return (
      <div className="profile-page flex items-center justify-center" style={{ minHeight: '60vh', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <Spin size="large" />
      </div>
    );
  }

  return (
    <div className="profile-page">
      {/* Top Banner with soft gradient */}
      <div className="profile-banner"></div>
      
      {/* Main Content Card */}
      <div className="profile-container">
        <Form
          form={form}
          layout="vertical"
          onFinish={handleFinish}
          className="profile-card"
        >
          
          {/* Header Profile Section */}
          <div className="profile-header">
            <div className="profile-info">
              <Upload
                customRequest={handleAvatarUpload}
                showUploadList={false}
                accept="image/png, image/jpeg, image/jpg"
                beforeUpload={beforeUpload}
              >
                <div className="profile-avatar-container" style={{ position: 'relative' }}>
                  <Avatar 
                    size={88} 
                    src={currentProfile?.avatarUrl}
                    icon={<UserOutlined />}
                    className="profile-avatar"
                  />
                  <div 
                    className="profile-avatar-upload-icon"
                    style={{
                      position: 'absolute',
                      bottom: 0,
                      right: 0,
                      background: '#fff',
                      borderRadius: '50%',
                      padding: '4px',
                      boxShadow: '0 2px 4px rgba(0,0,0,0.1)',
                      cursor: 'pointer',
                      border: '1px solid #f0f0f0'
                    }}
                  >
                    {uploading ? <Spin size="small" /> : <Camera size={16} color="#666" />}
                  </div>
                </div>
              </Upload>
              <div>
                <h1 className="profile-name">{currentProfile?.fullName || currentProfile?.username || 'Khách hàng'}</h1>
                <p className="profile-email">{currentProfile?.email}</p>
              </div>
            </div>
            <Button type="primary" htmlType="submit" loading={loading} className="profile-save-btn">
              Lưu thay đổi
            </Button>
          </div>

          {/* Form Grid */}
          <div className="profile-grid">
            {/* Họ và tên */}
            <Form.Item
              name="fullName"
              label={<span className="profile-field-label">Họ và tên</span>}
              rules={[{ required: true, message: 'Vui lòng nhập họ và tên' }]}
              style={{ marginBottom: 0 }}
            >
              <Input 
                placeholder="Nhập họ và tên" 
                size="large" 
                className="profile-field-input" 
              />
            </Form.Item>

            {/* Tên đăng nhập */}
            <Form.Item
              name="username"
              label={<span className="profile-field-label">Tên đăng nhập (Biệt danh)</span>}
              style={{ marginBottom: 0 }}
            >
              <Input 
                size="large" 
                className="profile-field-input" 
                disabled
              />
            </Form.Item>

            {/* Ngày sinh */}
            <Form.Item
              name="dateOfBirth"
              label={<span className="profile-field-label">Ngày sinh</span>}
              style={{ marginBottom: 0 }}
            >
              <DatePicker 
                placeholder="Chọn ngày sinh" 
                size="large" 
                className="profile-field-input w-full"
                style={{ width: '100%' }}
                format="DD/MM/YYYY"
              />
            </Form.Item>

            {/* Địa chỉ */}
            <Form.Item
              name="address"
              label={<span className="profile-field-label">Địa chỉ</span>}
              style={{ marginBottom: 0 }}
            >
              <Input 
                placeholder="Nhập địa chỉ của bạn" 
                size="large" 
                className="profile-field-input" 
              />
            </Form.Item>

            {/* Số điện thoại */}
            <Form.Item
              name="phone"
              label={<span className="profile-field-label">Số điện thoại</span>}
              style={{ marginBottom: 0 }}
            >
              <Input 
                size="large" 
                className="profile-field-input" 
                disabled
              />
            </Form.Item>
          </div>

          {/* Contact Section */}
          <div style={{ marginTop: '32px' }}>
            <h2 className="profile-emails-section-title">Thông tin liên hệ cố định</h2>
            
            <div className="profile-email-item" style={{ marginBottom: '16px' }}>
              <div className="profile-email-icon">
                <Mail size={18} />
              </div>
              <div>
                <p className="profile-email-address">{currentProfile?.email}</p>
                <p className="profile-email-time">Email đăng nhập</p>
              </div>
            </div>

            {currentProfile?.phone && (
              <div className="profile-email-item">
                <div className="profile-email-icon">
                  <Phone size={18} />
                </div>
                <div>
                  <p className="profile-email-address">{currentProfile?.phone}</p>
                  <p className="profile-email-time">Số điện thoại đăng ký</p>
                </div>
              </div>
            )}
          </div>

        </Form>
      </div>
    </div>
  );
};

export default ProfilePage;

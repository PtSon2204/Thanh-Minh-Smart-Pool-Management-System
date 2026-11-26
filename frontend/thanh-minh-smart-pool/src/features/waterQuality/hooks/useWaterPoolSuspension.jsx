import { useState } from 'react';
import { Input, Modal, message } from 'antd';
import { CheckCircleOutlined, WarningOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import poolAccessService from '../../tickets/services/poolAccessService';

export function useWaterPoolSuspension({ enabled = false } = {}) {
  const queryClient = useQueryClient();
  const [updatingPool, setUpdatingPool] = useState(false);

  const { data: liveStatus } = useQuery({
    queryKey: ['poolLiveStatus'],
    queryFn: poolAccessService.getLiveStatus,
    enabled,
  });

  const isPoolMaintenance = liveStatus?.operationalStatus === 'Maintenance';

  const invalidatePoolQueries = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['poolLiveStatus'] }),
      queryClient.invalidateQueries({ queryKey: ['poolOperationalConfig'] }),
    ]);
  };

  const confirmSuspendPool = (log) => {
    const measuredTime = log?.inspectionTime
      ? dayjs(log.inspectionTime).format('DD/MM/YYYY HH:mm')
      : dayjs().format('DD/MM/YYYY HH:mm');
    const phText = log?.phValue != null ? Number(log.phValue).toFixed(2) : '--';
    const cloText = log?.freeChlorineMgL != null ? Number(log.freeChlorineMgL).toFixed(2) : '--';
    const defaultReason = `Tạm đóng cửa để xử lý khẩn cấp chất lượng nước mức NGUY HIỂM (pH ${phText}, Clo ${cloText} mg/L lúc ${measuredTime}).`;
    let reasonDraft = defaultReason;

    Modal.confirm({
      title: 'Xác nhận tạm đóng cửa bể bơi để xử lý nước?',
      icon: <WarningOutlined style={{ color: '#cf1322' }} />,
      width: 520,
      content: (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          <p style={{ margin: 0 }}>
            Chỉ số đo lúc <strong>{measuredTime}</strong> đang ở mức <strong>NGUY HIỂM</strong> (pH:{' '}
            <strong>{phText}</strong>, Clo: <strong>{cloText} mg/L</strong>).
          </p>
          <p style={{ color: '#cf1322', fontSize: 13, margin: 0 }}>
            Khi xác nhận, hệ thống sẽ chuyển trạng thái bể sang <strong>Bảo trì (Maintenance)</strong>, khóa đặt vé online và hiện thông báo tạm ngưng đón khách tại trang chủ.
          </p>
          <div>
            <div style={{ fontSize: 12, fontWeight: 600, marginBottom: 4 }}>
              Lý do hiển thị cho khách hàng &amp; vận hành:
            </div>
            <Input.TextArea
              rows={2}
              maxLength={250}
              defaultValue={defaultReason}
              onChange={(e) => {
                reasonDraft = e.target.value;
              }}
            />
          </div>
        </div>
      ),
      okText: 'Xác nhận đóng cửa bể',
      cancelText: 'Giữ mở cửa & tiếp tục theo dõi',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          setUpdatingPool(true);
          const finalReason = reasonDraft?.trim() || defaultReason;
          await poolAccessService.toggleMaintenance('Maintenance', finalReason);
          message.success('Đã chuyển bể bơi sang trạng thái tạm đóng cửa để xử lý chất lượng nước.');
          await invalidatePoolQueries();
        } catch (err) {
          message.error(err?.response?.data?.message || 'Lỗi khi tạm đóng cửa bể bơi.');
        } finally {
          setUpdatingPool(false);
        }
      },
    });
  };

  const confirmResumePool = () => {
    Modal.confirm({
      title: 'Mở lại hoạt động đón khách bình thường?',
      icon: <CheckCircleOutlined style={{ color: '#16a34a' }} />,
      content: (
        <div>
          <p>
            Bạn có chắc chắn chất lượng nước và điều kiện an toàn đã đảm bảo để mở cửa đón khách trở lại?
          </p>
          <p style={{ color: '#15803d', fontSize: 13, marginBottom: 0 }}>
            Hệ thống sẽ gỡ banner tạm dừng ở trang chủ và mở lại tính năng đặt vé online.
          </p>
        </div>
      ),
      okText: 'Mở hoạt động bình thường',
      cancelText: 'Hủy bỏ',
      okButtonProps: {
        type: 'primary',
        style: { backgroundColor: '#16a34a', borderColor: '#16a34a' },
      },
      onOk: async () => {
        try {
          setUpdatingPool(true);
          await poolAccessService.toggleMaintenance('Operating', null);
          message.success('Đã mở lại hoạt động đón khách bình thường cho bể bơi.');
          await invalidatePoolQueries();
        } catch (err) {
          message.error(err?.response?.data?.message || 'Lỗi khi mở lại hoạt động bể bơi.');
        } finally {
          setUpdatingPool(false);
        }
      },
    });
  };

  return {
    liveStatus,
    isPoolMaintenance,
    updatingPool,
    confirmSuspendPool,
    confirmResumePool,
  };
}

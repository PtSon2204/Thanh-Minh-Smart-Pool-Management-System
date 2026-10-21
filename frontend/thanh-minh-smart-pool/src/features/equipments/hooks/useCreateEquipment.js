import { useMutation, useQueryClient } from '@tanstack/react-query'
import { message } from 'antd'
import equipmentService from '../services/equipmentService'

export function useCreateEquipment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: equipmentService.create,
    retry: false,
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['equipments'] })
      queryClient.invalidateQueries({ queryKey: ['equipment-filters'] })
      message.success(variables.cameraConnection?.protocol === 'Browser' ? 'Đã thêm camera máy tính. Mở Chi tiết để xem webcam.' : variables.cameraConnection ? 'Thêm thiết bị và kết nối camera thành công' : 'Thêm thiết bị thành công')
    },
  })
}

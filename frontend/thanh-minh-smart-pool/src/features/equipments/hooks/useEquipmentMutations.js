import { useMutation, useQueryClient } from '@tanstack/react-query'
import { message } from 'antd'
import equipmentService from '../services/equipmentService'

function useEquipmentMutation(mutationFn, successMessage) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    retry: false,
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['equipments'] })
      queryClient.invalidateQueries({ queryKey: ['equipment-filters'] })
      message.success(typeof successMessage === 'function' ? successMessage(variables) : successMessage)
    },
  })
}

export function useUpdateEquipment() {
  return useEquipmentMutation(({ id, ...payload }) => equipmentService.update(id, payload), 'Cập nhật thiết bị thành công')
}

export function useSetEquipmentStatus() {
  return useEquipmentMutation(({ id, status }) => equipmentService.setStatus(id, status), variables => variables.status === 'Active' ? 'Mở khóa thiết bị thành công' : 'Khóa thiết bị thành công')
}

import apiClient from '../../../services/apiClient'

const BASE = '/api/roles'

const rolePermissionService = {
  getCatalog: () => apiClient.get(`${BASE}/permissions/catalog`).then((response) => response.data),
  getRoles: () => apiClient.get(BASE).then((response) => response.data),
  getPermissions: (id) => apiClient.get(`${BASE}/${id}/permissions`).then((response) => response.data),
  changePermission: (id, module, action, enabled) => apiClient.patch(`${BASE}/${id}/permissions`, {
    module,
    action,
    enabled,
  }).then((response) => response.data),
}

export default rolePermissionService

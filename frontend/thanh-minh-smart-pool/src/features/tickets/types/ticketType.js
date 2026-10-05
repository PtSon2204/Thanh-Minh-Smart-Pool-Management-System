/**
 * Enum phân loại vé — phải khớp với TicketCategoryEnum ở Backend.
 */
export const TicketCategory = Object.freeze({
  VE_THANG: 'VE_THANG',
  VE_LUOT:  'VE_LUOT',
})

/**
 * Label tiếng Việt hiển thị trên UI.
 */
export const TicketCategoryLabel = Object.freeze({
  VE_THANG:  'Vé tháng',
  VE_LUOT:   'Vé lượt',
})

/**
 * Màu badge cho từng loại vé (Ant Design color tokens).
 */
export const TicketCategoryColor = Object.freeze({
  VE_THANG:  'blue',
  VE_LUOT:   'green',
})


export const TicketCategoryOptions = [
  { value: TicketCategory.VE_THANG,  label: TicketCategoryLabel.VE_THANG  },
  { value: TicketCategory.VE_LUOT,   label: TicketCategoryLabel.VE_LUOT   },
]

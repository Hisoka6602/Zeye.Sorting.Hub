import type { ThemeConfig } from 'antd';

export const theme: ThemeConfig = {
  token: {
    colorPrimary: '#1677ff', colorInfo: '#1677ff', colorSuccess: '#13a66d',
    colorError: '#e8484a', colorWarning: '#e6a023', colorText: '#13213f',
    colorTextSecondary: '#61708d', colorTextPlaceholder: '#abb8ce', colorBorder: '#d3ddeb', colorBgLayout: '#f3f5f7',
    borderRadius: 6,
    fontFamily: 'Microsoft YaHei UI, Microsoft YaHei, PingFang SC, Segoe UI, sans-serif',
    fontSize: 15, controlHeight: 40,
  },
  components: {
    Button: { primaryShadow: 'none', colorPrimary: '#076dff', colorPrimaryHover: '#2885ff', colorPrimaryActive: '#0059df' },
    Input: { lineWidth: 1.5 },
    InputNumber: { lineWidth: 1.5 },
    Select: { lineWidth: 1.5 },
    DatePicker: { lineWidth: 1.5 },
    Checkbox: { lineWidth: 1.5 },
    Card: { headerBg: '#fff' },
    Table: { headerBg: '#f4f5f6', headerColor: '#162440', borderColor: '#e0e7f2', lineWidth: 1.5 },
    Menu: { itemHeight: 46, subMenuItemBg: 'transparent', itemSelectedBg: '#e6f0ff', itemSelectedColor: '#0964e8' },
  },
};

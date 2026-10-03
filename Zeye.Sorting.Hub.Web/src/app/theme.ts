import type { ThemeConfig } from 'antd';
import { typography } from './typography';

export const theme: ThemeConfig = {
  token: {
    colorPrimary: '#1677ff', colorInfo: '#1677ff', colorSuccess: '#13a66d',
    colorError: '#e8484a', colorWarning: '#e6a023', colorText: typography.color.body,
    colorTextHeading: typography.color.heading, colorTextSecondary: typography.color.secondary,
    colorTextDescription: typography.color.secondary, colorTextPlaceholder: '#abb8ce', colorBorder: '#d3ddeb', colorBgLayout: '#f3f5f7',
    borderRadius: 6,
    fontFamily: typography.fontFamily, fontWeightStrong: typography.weight.semibold,
    fontSize: typography.size.body, lineHeight: 1.6, controlHeight: 40,
  },
  components: {
    Button: { primaryShadow: 'none', colorPrimary: '#076dff', colorPrimaryHover: '#2885ff', colorPrimaryActive: '#0059df' },
    Input: { lineWidth: 1.5 },
    InputNumber: { lineWidth: 1.5 },
    Select: { lineWidth: 1.5 },
    DatePicker: { lineWidth: 1.5 },
    Checkbox: { lineWidth: 1.5 },
    Form: { labelFontSize: typography.size.label, labelColor: typography.color.heading },
    Card: { headerBg: '#fff', headerFontSize: typography.size.section },
    Table: { headerBg: '#f4f5f6', headerColor: typography.color.heading, borderColor: '#e0e7f2', lineWidth: 1.5 },
    Modal: { titleFontSize: typography.size.section },
    Menu: { itemHeight: 46, subMenuItemBg: 'transparent', itemSelectedBg: '#e6f0ff', itemSelectedColor: '#0964e8' },
  },
};

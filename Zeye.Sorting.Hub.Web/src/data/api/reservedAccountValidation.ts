/** 程序内置账号名不能用于初始化、创建或重命名普通账号。 */
export const reservedAccountRule = {
  validator: (_: unknown, value: unknown) => typeof value === 'string' && value.trim().toLowerCase() === 'hisoka'
    ? Promise.reject(new Error('hisoka 为内置账号名，请使用其他账号名')) : Promise.resolve(),
};

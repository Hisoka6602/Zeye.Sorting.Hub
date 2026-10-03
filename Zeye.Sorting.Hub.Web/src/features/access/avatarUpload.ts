/** 将头像居中裁为方形，保留 PNG 透明度，较大图片转为 JPEG 后保存。 */
export async function prepareAvatar(file: File): Promise<string> {
  if (!['image/png', 'image/jpeg'].includes(file.type)) throw new Error('请选择 JPG 或 PNG 图片');
  if (file.size > 5 * 1024 * 1024) throw new Error('图片大小不能超过 5 MB');
  let image: ImageBitmap;
  try { image = await createImageBitmap(file); }
  catch { throw new Error('无法读取这张图片，请选择有效的 JPG 或 PNG 图片'); }
  try {
    const canvas = document.createElement('canvas'); canvas.width = canvas.height = 256;
    const context = canvas.getContext('2d');
    if (!context) throw new Error('浏览器无法处理图片，请更新浏览器后重试');
    const edge = Math.min(image.width, image.height);
    if (!edge) throw new Error('图片尺寸无效');
    const draw = () => context.drawImage(image, (image.width - edge) / 2, (image.height - edge) / 2, edge, edge, 0, 0, 256, 256);
    draw();
    const png = canvas.toDataURL('image/png');
    if (png.length <= 131094) return png;
    context.fillStyle = '#fff'; context.fillRect(0, 0, 256, 256); draw();
    const jpeg = canvas.toDataURL('image/jpeg', .85);
    if (jpeg.length > 131095) throw new Error('图片处理后仍过大，请选择其他头像');
    return jpeg;
  } finally { image.close(); }
}

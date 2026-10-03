import type { CSSProperties } from 'react';
import brandSymbolUrl from '../assets/brand-symbol.svg?no-inline';

/** 中文说明：页面品牌和标签页共用 SVG 图形，保留各场景配色。 */
function BrandSymbol({ color = '#1677ff' }: { color?: string }) {
  return <use href={`${brandSymbolUrl}#brand-symbol`} color={color} />;
}

export function BrandMark({ className = '', variant = 'sidebar' }: { className?: string; variant?: 'sidebar' | 'login' }) {
  return <svg className={`brand-mark hub-brand-mark ${className}`} viewBox="0 0 24 28"
    width={variant === 'login' ? 42 : 23} height={variant === 'login' ? 48 : 26}
    aria-hidden="true" focusable="false"><BrandSymbol /></svg>;
}

export function BrandWordmark({ className = '', style, inkBounds, variant = 'header' }: { className?: string; style?: CSSProperties; inkBounds?: { x: number; y: number; width: number; height: number }; variant?: 'header' | 'login' }) {
  if (variant === 'login') {
    return <svg className={className} viewBox="0 0 304 49" width="304" height="49"
      role="img" aria-label="Zeye Sorting Hub" focusable="false" style={style}>
      <g transform="scale(1.75)"><BrandSymbol color="#3e6eff" /></g>
      <text x="60" y="36" fontFamily="Segoe UI, Arial, sans-serif" fontSize="30" fontWeight="700"
        textLength="244" lengthAdjust="spacingAndGlyphs" fill="#101729">
        <tspan fill="#3e6eff">Zeye</tspan><tspan> Sorting Hub</tspan>
      </text>
    </svg>;
  }
  const width = typeof style?.width === 'number' ? style.width : 195;
  const height = typeof style?.height === 'number' ? style.height : 28;
  const scaleX = width / 195, scaleY = height / 28;
  const ink = inkBounds ?? { x: 36, y: 6, width: 158, height: 19 };
  const textX = (ink.x - .7) / scaleX;
  const textY = (ink.y + ink.height * 14.5 / 19) / scaleY;
  const fontSize = ink.height / scaleY;
  const textLength = (ink.width + 1.5) / scaleX;
  return <svg className={className} viewBox="0 0 195 28" width="195" height="28" preserveAspectRatio="none"
    role="img" aria-label="Zeye Sorting Hub" focusable="false" style={style}>
    <BrandSymbol />
    <text x={textX} y={textY} fontFamily="Segoe UI, Arial, sans-serif" fontSize={fontSize} fontWeight="700"
      textLength={textLength} lengthAdjust="spacingAndGlyphs" fill="#1d254b">
      <tspan fill="#1677ff">Zeye</tspan><tspan> Sorting Hub</tspan>
    </text>
  </svg>;
}

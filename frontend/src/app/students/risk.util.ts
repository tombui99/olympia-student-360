import { RiskLevel } from './students.service';

export function riskBadgeClass(level: RiskLevel | string): string {
  switch (level) {
    case 'High':
      return 'bg-red-50 text-red-700 border border-red-200';
    case 'Medium':
      return 'bg-[#fff8e1] text-[#7a5b12] border border-[#efe0b0]';
    case 'Low':
      return 'bg-[#f1edf4] text-[#5b287d] border border-[#e0d4ea]';
    default:
      return 'bg-[#eef6ee] text-[#3f6f24] border border-[#d5e8cf]';
  }
}

export function riskDotClass(level: RiskLevel | string): string {
  switch (level) {
    case 'High':
      return 'bg-red-500';
    case 'Medium':
      return 'bg-[#e0a92e]';
    case 'Low':
      return 'bg-[#8a63b0]';
    default:
      return 'bg-emerald-500';
  }
}

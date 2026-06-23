import { FormControl } from '@angular/forms';
import { futureDateValidator } from './future-date.validator';

describe('futureDateValidator', () => {
  const validator = futureDateValidator();

  it('should return null for an empty control', () => {
    expect(validator(new FormControl(''))).toBeNull();
    expect(validator(new FormControl(null))).toBeNull();
  });

  it('should return null for today', () => {
    const today = new Date();
    today.setHours(12, 0, 0, 0);
    const iso = today.toISOString().split('T')[0];
    expect(validator(new FormControl(iso))).toBeNull();
  });

  it('should return null for a future date', () => {
    const future = new Date();
    future.setDate(future.getDate() + 7);
    const iso = future.toISOString().split('T')[0];
    expect(validator(new FormControl(iso))).toBeNull();
  });

  it('should return { pastDate: true } for yesterday', () => {
    const yesterday = new Date();
    yesterday.setDate(yesterday.getDate() - 1);
    const iso = yesterday.toISOString().split('T')[0];
    expect(validator(new FormControl(iso))).toEqual({ pastDate: true });
  });

  it('should return { pastDate: true } for a date far in the past', () => {
    expect(validator(new FormControl('2020-01-01'))).toEqual({ pastDate: true });
  });
});

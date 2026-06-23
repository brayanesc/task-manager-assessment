import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Rejects dates that are strictly in the past (today is allowed). */
export function futureDateValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    if (!control.value) return null;
    // Append T00:00:00 (no Z) so the browser parses the date in local time,
    // not UTC — otherwise YYYY-MM-DD is treated as UTC midnight which falls
    // "in the past" for users in UTC-negative timezones.
    const picked = new Date(`${control.value}T00:00:00`);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return picked >= today ? null : { pastDate: true };
  };
}

import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Rejects dates that are strictly in the past (today is allowed). */
export function futureDateValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    if (!control.value) return null;
    const picked = new Date(control.value);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return picked >= today ? null : { pastDate: true };
  };
}

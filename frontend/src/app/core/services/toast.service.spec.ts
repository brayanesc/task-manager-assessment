import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ToastService } from './toast.service';

describe('ToastService', () => {
  let service: ToastService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ToastService);
  });

  it('should start with an empty toast list', () => {
    expect(service.toasts()).toEqual([]);
  });

  it('should add a toast with the correct message and type on show()', () => {
    service.show('Upload complete', 'success');

    expect(service.toasts().length).toBe(1);
    expect(service.toasts()[0].message).toBe('Upload complete');
    expect(service.toasts()[0].type).toBe('success');
  });

  it('should default the type to info when not specified', () => {
    service.show('Hello');
    expect(service.toasts()[0].type).toBe('info');
  });

  it('should remove the toast when dismiss() is called with its id', () => {
    service.show('To be dismissed', 'error');
    const { id } = service.toasts()[0];

    service.dismiss(id);

    expect(service.toasts()).toEqual([]);
  });

  it('should not affect other toasts when one is dismissed', () => {
    service.show('First', 'info');
    service.show('Second', 'success');
    const firstId = service.toasts()[0].id;

    service.dismiss(firstId);

    expect(service.toasts().length).toBe(1);
    expect(service.toasts()[0].message).toBe('Second');
  });

  it('should auto-dismiss the toast after the specified duration', fakeAsync(() => {
    service.show('Auto-dismiss me', 'info', 3000);
    expect(service.toasts().length).toBe(1);

    tick(3000);

    expect(service.toasts().length).toBe(0);
  }));
});

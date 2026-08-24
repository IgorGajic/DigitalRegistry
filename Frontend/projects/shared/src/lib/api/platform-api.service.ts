import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../config/tokens';
import {
  CreatedUserDto,
  LicenseDto,
  LicensePaymentDto,
  PlatformDashboardDto,
  RestaurantSummaryDto,
} from '../models/dtos';
import { LicensePlan, LicenseStatus, PaymentMethod } from '../models/enums';

/** Every call the master application makes. */
@Injectable({ providedIn: 'root' })
export class PlatformApiService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  dashboard(revenueMonths = 12): Observable<PlatformDashboardDto> {
    return this.http.get<PlatformDashboardDto>(`${this.base}/api/platform/dashboard`, {
      params: new HttpParams().set('revenueMonths', revenueMonths),
    });
  }

  restaurants(filters: {
    search?: string;
    licenseStatus?: LicenseStatus | null;
    isActive?: boolean | null;
  } = {}): Observable<RestaurantSummaryDto[]> {
    let params = new HttpParams();

    if (filters.search) {
      params = params.set('search', filters.search);
    }

    if (filters.licenseStatus != null) {
      params = params.set('licenseStatus', filters.licenseStatus);
    }

    if (filters.isActive != null) {
      params = params.set('isActive', filters.isActive);
    }

    return this.http.get<RestaurantSummaryDto[]>(`${this.base}/api/platform/restaurants`, { params });
  }

  restaurant(id: string): Observable<RestaurantSummaryDto> {
    return this.http.get<RestaurantSummaryDto>(`${this.base}/api/platform/restaurants/${id}`);
  }

  createRestaurant(body: {
    name: string;
    slug: string;
    address?: string | null;
    contactEmail?: string | null;
    phoneNumber?: string | null;
    currencyCode?: string | null;
    timeZoneId?: string | null;
  }): Observable<RestaurantSummaryDto> {
    return this.http.post<RestaurantSummaryDto>(`${this.base}/api/platform/restaurants`, body);
  }

  updateRestaurant(
    id: string,
    body: {
      name: string;
      address?: string | null;
      contactEmail?: string | null;
      phoneNumber?: string | null;
      currencyCode?: string | null;
      timeZoneId?: string | null;
    },
  ): Observable<RestaurantSummaryDto> {
    return this.http.put<RestaurantSummaryDto>(`${this.base}/api/platform/restaurants/${id}`, {
      id,
      ...body,
    });
  }

  setRestaurantActive(id: string, active: boolean): Observable<RestaurantSummaryDto> {
    return this.http.post<RestaurantSummaryDto>(
      `${this.base}/api/platform/restaurants/${id}/${active ? 'activate' : 'suspend'}`,
      {},
    );
  }

  createOwner(
    restaurantId: string,
    body: { email: string; password: string; firstName: string; lastName: string },
  ): Observable<CreatedUserDto> {
    return this.http.post<CreatedUserDto>(
      `${this.base}/api/platform/restaurants/${restaurantId}/owner`,
      { restaurantId, ...body },
    );
  }

  licenses(restaurantId?: string, status?: LicenseStatus | null): Observable<LicenseDto[]> {
    let params = new HttpParams();

    if (restaurantId) {
      params = params.set('restaurantId', restaurantId);
    }

    if (status != null) {
      params = params.set('status', status);
    }

    return this.http.get<LicenseDto[]>(`${this.base}/api/platform/licenses`, { params });
  }

  issueLicense(body: {
    restaurantId: string;
    plan: LicensePlan;
    price: number;
    notes?: string | null;
  }): Observable<LicenseDto> {
    return this.http.post<LicenseDto>(`${this.base}/api/platform/licenses`, body);
  }

  /** Extends from the current expiry when the term has not lapsed, so paying early costs nothing. */
  renewLicense(
    licenseId: string,
    body: { plan: LicensePlan; price: number; notes?: string | null },
  ): Observable<LicenseDto> {
    return this.http.post<LicenseDto>(`${this.base}/api/platform/licenses/${licenseId}/renew`, {
      licenseId,
      ...body,
    });
  }

  suspendLicense(licenseId: string, reason: string): Observable<LicenseDto> {
    return this.http.post<LicenseDto>(`${this.base}/api/platform/licenses/${licenseId}/suspend`, {
      reason,
    });
  }

  reactivateLicense(licenseId: string): Observable<LicenseDto> {
    return this.http.post<LicenseDto>(
      `${this.base}/api/platform/licenses/${licenseId}/reactivate`,
      {},
    );
  }

  cancelLicense(licenseId: string, reason: string): Observable<LicenseDto> {
    return this.http.post<LicenseDto>(`${this.base}/api/platform/licenses/${licenseId}/cancel`, {
      reason,
    });
  }

  licensePayments(licenseId: string): Observable<LicensePaymentDto[]> {
    return this.http.get<LicensePaymentDto[]>(
      `${this.base}/api/platform/licenses/${licenseId}/payments`,
    );
  }

  recordPayment(
    licenseId: string,
    body: {
      amount: number;
      paidAtUtc?: string | null;
      paymentMethod: PaymentMethod;
      referenceNumber?: string | null;
      notes?: string | null;
    },
  ): Observable<LicensePaymentDto> {
    return this.http.post<LicensePaymentDto>(
      `${this.base}/api/platform/licenses/${licenseId}/payments`,
      { licenseId, ...body },
    );
  }
}

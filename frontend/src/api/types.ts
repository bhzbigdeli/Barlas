// هم‌شکل DTOهای بک‌اند (Barlas.Application). enumها به‌صورت رشته می‌آیند.

export type Permission =
  | "ViewAllUnits"
  | "ManageUsers"
  | "ManageCatalog"
  | "ImportOrders"
  | "ManagePlans"
  | "ApproveSurplus"
  | "ScanStages"
  | "ManageWarehouse"
  | "ViewReports";

export interface UserInfo {
  id: string;
  userName: string;
  fullName: string;
  roleId: string;
  roleName: string;
  unitId: string | null;
  unitName: string | null;
  seesAllUnits: boolean;
  permissions: Permission[];
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: UserInfo;
}

export interface Unit {
  id: string;
  code: string;
  name: string;
}

export type SalesOrderStatus = "Received" | "Planned" | "InProduction" | "ReadyToShip" | "Shipped" | "Cancelled";

export const orderStatusLabels: Record<SalesOrderStatus, string> = {
  Received: "دریافت‌شده",
  Planned: "برنامه‌ریزی‌شده",
  InProduction: "در تولید",
  ReadyToShip: "آماده ارسال",
  Shipped: "ارسال‌شده",
  Cancelled: "لغوشده",
};

export interface OrderSummary {
  id: string;
  unitId: string;
  unitName: string;
  externalNumber: string;
  orderDate: string;
  dueDate: string | null;
  customerName: string | null;
  status: SalesOrderStatus;
  totalPairs: number;
  fulfilledPairs: number;
}

export interface OrderLineSize {
  size: number;
  quantity: number;
  fromStock: number;
  fromProduction: number;
  outstanding: number;
}

export interface OrderLine {
  lineNumber: number;
  modelCode: string;
  modelName: string;
  colorName: string;
  sizes: OrderLineSize[];
}

export interface OrderDetail {
  summary: OrderSummary;
  lines: OrderLine[];
}

export interface IntakeResult {
  created: number;
  skipped: number;
  errors: string[];
}

export interface BundleState {
  bundleId: string;
  number: string;
  modelName: string;
  colorName: string;
  pairs: number;
  status: "Issued" | "InProgress" | "Finished" | "InWarehouse";
  lastStage: string | null;
  nextStage: string | null;
}

// --- مدیریت کاربران و سطوح دسترسی ---

export interface PermissionInfo {
  permission: Permission;
  name: string;
  description: string;
}

export interface Role {
  id: string;
  name: string;
  isSystem: boolean;
  permissions: Permission[];
  userCount: number;
}

export interface UserRow {
  id: string;
  userName: string;
  fullName: string;
  roleId: string;
  roleName: string;
  unitId: string | null;
  unitName: string | null;
  isActive: boolean;
}

// --- اطلاعات پایه ---

export interface SeriesSize {
  size: number;
  quantity: number;
}

export interface Product {
  id: string;
  code: string;
  name: string;
  routeId: string;
  routeName: string;
  soleId: string | null;
  soleName: string | null;
  isActive: boolean;
  series: SeriesSize[];
  pairsPerSeries: number;
}

export interface CodedItem {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
}

export interface ProductionRoute {
  id: string;
  name: string;
  stages: string[];
}

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

export type SalesOrderStatus =
  | "Received"
  | "Planned"
  | "InProduction"
  | "ReadyToShip"
  | "Shipped"
  | "PartiallyShipped"
  | "Cancelled";

export const orderStatusLabels: Record<SalesOrderStatus, string> = {
  Received: "دریافت‌شده",
  Planned: "برنامه‌ریزی‌شده",
  InProduction: "در تولید",
  ReadyToShip: "آماده ارسال",
  Shipped: "ارسال‌شده",
  PartiallyShipped: "ارسال جزئی",
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
  shippedPairs: number;
}

export interface OrderLineSize {
  size: number;
  quantity: number;
  fromStock: number;
  fromProduction: number;
  outstanding: number;
  shipped: number;
  readyToShip: number;
}

export interface OrderLine {
  id: string;
  lineNumber: number;
  modelCode: string;
  modelName: string;
  colorName: string;
  sizes: OrderLineSize[];
}

export interface OrderShipment {
  number: string;
  shippedAtUtc: string;
  pairs: number;
}

export interface OrderDetail {
  summary: OrderSummary;
  lines: OrderLine[];
  shipments: OrderShipment[];
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

export interface PieceRate {
  stageName: string;
  ratePerPair: number;
  effectiveFrom: string;
}

export interface Product {
  id: string;
  code: string;
  name: string;
  group: string | null;
  routeId: string;
  routeName: string;
  soleId: string | null;
  soleName: string | null;
  isActive: boolean;
  series: SeriesSize[];
  pairsPerSeries: number;
  rates: PieceRate[];
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

// --- برنامه‌ریزی، دسته‌ها، انبار ---

export type PlanStatus = "Draft" | "AwaitingApproval" | "Approved" | "Released" | "Completed" | "Cancelled";

export const planStatusLabels: Record<PlanStatus, string> = {
  Draft: "پیش‌نویس",
  AwaitingApproval: "در انتظار تأیید مازاد",
  Approved: "تأییدشده",
  Released: "صادرشده (در تولید)",
  Completed: "تکمیل‌شده",
  Cancelled: "لغوشده",
};

export type BundleStatus = BundleState["status"];

export const bundleStatusLabels: Record<BundleStatus, string> = {
  Issued: "صادرشده",
  InProgress: "در جریان",
  Finished: "تمام‌شده",
  InWarehouse: "در انبار",
};

export interface PlanningRow {
  salesOrderLineId: string;
  shoeModelId: string;
  modelCode: string;
  modelName: string;
  colorId: string;
  colorName: string;
  size: number;
  ordered: number;
  fromStock: number;
  alreadyPlanned: number;
  unplanned: number;
}

export interface PlanSummary {
  id: string;
  number: string;
  status: PlanStatus;
  orderPairs: number;
  surplusPairs: number;
  bundleCount: number;
}

export interface OrderPlanning {
  orderId: string;
  externalNumber: string;
  status: SalesOrderStatus;
  surplusThreshold: number;
  rows: PlanningRow[];
  plans: PlanSummary[];
}

export interface PlanLine {
  modelCode: string;
  modelName: string;
  colorName: string;
  size: number;
  orderQuantity: number;
  surplusQuantity: number;
}

export interface Bundle {
  id: string;
  number: string;
  qrToken: string;
  modelCode: string;
  modelName: string;
  colorName: string;
  sizes: SeriesSize[];
  pairs: number;
  status: BundleStatus;
  lastStage: string | null;
  nextStage: string | null;
  route: string[];
}

export interface PlanDetail {
  id: string;
  number: string;
  unitId: string;
  unitName: string;
  salesOrderId: string | null;
  orderNumber: string | null;
  status: PlanStatus;
  surplusRatio: number;
  surplusThreshold: number;
  lines: PlanLine[];
  bundles: Bundle[];
}

export interface ReceiveResult {
  bundleNumber: string;
  toOrder: number;
  toFreeStock: number;
  orderNumber: string | null;
  orderStatus: SalesOrderStatus | null;
}

export interface FreeStockRow {
  unitName: string;
  modelCode: string;
  modelName: string;
  colorName: string;
  size: number;
  quantity: number;
}

export interface Worker {
  id: string;
  unitId: string;
  unitName: string;
  personnelCode: string;
  fullName: string;
  isActive: boolean;
}

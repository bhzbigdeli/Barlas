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
  description: string | null;
  imageUploadedAtUtc: string | null;
}

export interface ProductOverview {
  product: Product;
  routeStages: string | null;
  orderedPairs: number;
  producedPairs: number;
  shippedPairs: number;
  freeStockPairs: number;
  laborPerPair: number;
  orders: {
    orderId: string;
    orderNumber: string;
    customerName: string | null;
    colorName: string;
    pairs: number;
    shippedPairs: number;
    status: SalesOrderStatus;
    orderDate: string;
    dueDate: string | null;
  }[];
  plans: { planId: string; planNumber: string; status: PlanStatus; pairs: number; plannedEndDate: string | null; completedAtUtc: string | null }[];
  stock: { unitName: string; colorName: string; size: number; quantity: number }[];
  wip: { stageName: string; bundles: number; pairs: number }[];
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

// --- نمای کامل زیربرنامه ---

export interface Milestone {
  key: string;
  title: string;
  plannedDate: string | null;
  actualAtUtc: string | null;
  delayDays: number | null;
}

export interface StageProgress {
  stageId: string;
  name: string;
  plannedDate: string | null;
  pairsDone: number;
  pairsTotal: number;
  percent: number;
  firstAtUtc: string | null;
  finishedAtUtc: string | null;
  delayDays: number | null;
}

export interface BundleCell {
  stageId: string;
  inRoute: boolean;
  doneAtUtc: string | null;
  workerName: string | null;
  reworkCount: number;
}

export interface BundleProgress {
  id: string;
  number: string;
  modelCode: string;
  colorName: string;
  pairs: number;
  status: BundleStatus;
  percent: number;
  nextStage: string | null;
  cells: BundleCell[];
  finishedAtUtc: string | null;
  inWarehouseAtUtc: string | null;
}

export interface PlanOverview {
  id: string;
  number: string;
  status: PlanStatus;
  unitName: string;
  salesOrderId: string | null;
  orderNumber: string | null;
  customerName: string | null;
  orderDueDate: string | null;
  orderStatus: SalesOrderStatus | null;
  orderPairs: number;
  surplusPairs: number;
  totalPairs: number;
  pairsInWarehouse: number;
  percent: number;
  plannedStartDate: string | null;
  plannedEndDate: string | null;
  actualStartUtc: string | null;
  actualEndUtc: string | null;
  delayDays: number | null;
  laborCost: number;
  milestones: Milestone[];
  stages: StageProgress[];
  bundles: BundleProgress[];
  workers: { name: string; personnelCode: string; pairs: number; wage: number }[];
}

// --- گزارش‌ها ---

export interface ReportSummary {
  range: { from: string; to: string };
  ordersReceived: number;
  pairsOrdered: number;
  pairsProduced: number;
  pairsShipped: number;
  ordersShipped: number;
  ordersShippedOnTime: number;
  onTimePercent: number | null;
  avgLeadDays: number | null;
  openOrders: number;
  lateOpenOrders: number;
  pairsInProgress: number;
  laborCost: number;
}

export interface DelayReport {
  orders: {
    id: string;
    number: string;
    customerName: string | null;
    unitName: string;
    status: SalesOrderStatus;
    pairs: number;
    shippedPairs: number;
    dueDate: string | null;
    shippedAtUtc: string | null;
    delayDays: number;
  }[];
  atRisk: { id: string; number: string; customerName: string | null; dueDate: string; daysLeft: number; pairs: number; readyPairs: number }[];
  plans: {
    id: string;
    number: string;
    orderNumber: string | null;
    status: PlanStatus;
    percent: number;
    plannedEndDate: string | null;
    completedAtUtc: string | null;
    delayDays: number;
  }[];
  stages: { planId: string; planNumber: string; stageName: string; plannedDate: string; pairsDone: number; pairsTotal: number; delayDays: number }[];
}

export interface OrderTimeline {
  id: string;
  number: string;
  customerName: string | null;
  unitName: string;
  status: SalesOrderStatus;
  pairs: number;
  shippedPairs: number;
  orderDate: string;
  receivedAtUtc: string;
  plannedAtUtc: string | null;
  inProductionAtUtc: string | null;
  readyAtUtc: string | null;
  firstShipmentAtUtc: string | null;
  shippedAtUtc: string | null;
  dueDate: string | null;
  delayDays: number | null;
  leadDays: number | null;
}

export interface DailyProduction {
  stages: string[];
  days: { date: string; pairsByStage: Record<string, number>; total: number }[];
  totals: Record<string, number>;
}

export interface WorkerOutput {
  workerId: string;
  personnelCode: string;
  name: string;
  unitName: string;
  scans: number;
  pairs: number;
  reworks: number;
  activeDays: number;
  wage: number;
  pairsByStage: Record<string, number>;
}

export interface WipReport {
  stages: { stageName: string; bundles: number; pairs: number; avgWaitDays: number; maxWaitDays: number }[];
  oldest: {
    planId: string;
    planNumber: string;
    bundleNumber: string;
    modelCode: string;
    colorName: string;
    pairs: number;
    waiting: string;
    sinceUtc: string;
    waitDays: number;
  }[];
}

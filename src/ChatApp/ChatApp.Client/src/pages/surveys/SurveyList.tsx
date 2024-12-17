
import { Stack } from '@fluentui/react';
import { ShieldLockRegular } from '@fluentui/react-icons';
import { useContext, useEffect, useState } from 'react';
import {
    getSurveys,
    getUserInfo, Survey
} from "../../api";
import { AppStateContext } from "../../state/AppProvider";
import styles from './Survey.module.css';
import { Link } from 'react-router-dom';
import { DetailsList, DetailsListLayoutMode, IColumn, SelectionMode } from '@fluentui/react/lib/DetailsList';

const SurveyList = () => {
    const appStateContext = useContext(AppStateContext)
    const AUTH_ENABLED = appStateContext?.state.frontendSettings?.auth_enabled
    const [showAuthMessage, setShowAuthMessage] = useState<boolean | undefined>()
    const [surveys, setSurveys] = useState<Survey[]>([]);
    // todo: add in loading/error display
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    useEffect(() => {
        const fetchSurveyData = async () => {
            try {
                const response = await getSurveys();
                setSurveys(response);
                setError(null);
            } catch (err: any) {
                setError(err?.message);
                setSurveys([]);
            } finally {
                setLoading(false);
            }
        };

        fetchSurveyData();
    }, []);

    const getUserInfoList = async () => {
        if (!AUTH_ENABLED) {
            setShowAuthMessage(false)
            return
        }
        const userInfoList = await getUserInfo()
        if (userInfoList.length === 0 && window.location.hostname !== '127.0.0.1') {
            setShowAuthMessage(true)
        } else {
            setShowAuthMessage(false)
        }
    }

    const renderItemColumn = (item?: any, index?: number | undefined, column?: IColumn | undefined): React.ReactNode | undefined => {
        if (column === undefined) return undefined;
        if (item === undefined) return undefined;

        const fieldContent = item[column.fieldName as keyof Survey] as string;

        switch (column.key) {
            case 'id':
                var ref = `/chat/${fieldContent}`;
                return <Link to={ref}>{fieldContent}</Link>;
            case 'filename':
                return <span>{fieldContent}</span>;
            case 'version':
                return <span>{fieldContent}</span>;
            default:
                return <span>{fieldContent}</span>;
        }
    }

    const columns = [
        { key: 'id', name: 'ID', fieldName: 'id', minWidth: 100, maxWidth: 200, isResizable: true },
        { key: 'filename', name: 'Name', fieldName: 'filename', minWidth: 400, maxWidth: 1000, isResizable: true },
        { key: 'version', name: 'Version', fieldName: 'version', minWidth: 100, maxWidth: 200, isResizable: true }
    ];

    return (
        <div className={styles.container} role="main">
            {showAuthMessage ? (
                <Stack className={styles.emptyState}>
                    <ShieldLockRegular
                        className={styles.icon}
                        style={{ color: 'darkorange', height: '200px', width: '200px' }}
                    />
                    <h1 className={styles.emptyStateTitle}>Authentication Not Configured</h1>
                    <h2 className={styles.emptyStateSubtitle}>
                        This app does not have authentication configured. Please add an identity provider by finding your app in the{' '}
                        <a href="https://portal.azure.com/" target="_blank">
                            Azure Portal
                        </a>
                        and following{' '}
                        <a
                            href="https://learn.microsoft.com/en-us/azure/app-service/scenario-secure-app-authentication-app-service#3-configure-authentication-and-authorization"
                            target="_blank">
                            these instructions
                        </a>
                        .
                    </h2>
                    <h2 className={styles.emptyStateSubtitle} style={{ fontSize: '20px' }}>
                        <strong>Authentication configuration takes a few minutes to apply. </strong>
                    </h2>
                    <h2 className={styles.emptyStateSubtitle} style={{ fontSize: '20px' }}>
                        <strong>If you deployed in the last 10 minutes, please wait and reload the page after 10 minutes.</strong>
                    </h2>
                </Stack>
            ) : (
                <Stack horizontal className={styles.root}>
                    <DetailsList
                        compact={true}
                        items={surveys}
                        columns={columns}
                        setKey="set"
                        layoutMode={DetailsListLayoutMode.justified}
                        selectionMode={SelectionMode.none}
                        onRenderItemColumn={renderItemColumn}
                    />
                </Stack>
            )}
        </div>
    )
}

export default SurveyList;
